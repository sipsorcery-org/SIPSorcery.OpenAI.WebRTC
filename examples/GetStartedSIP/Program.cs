//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: An example SIP to WebRTC application that can be used to interact with
// OpenAI's real-time API https://platform.openai.com/docs/guides/realtime-webrtc.
//
// Usage:
// set OPENAI_API_KEY=your_openai_key
// dotnet run
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 19 Dec 2024	Aaron Clauson	Created, Wexford, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License and the additional
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Serilog;
using SIPSorceryMedia.Windows;
using Serilog.Extensions.Logging;
using Microsoft.Extensions.Logging;
using SIPSorcery.OpenAIWebRTC;
using SIPSorcery.OpenAIWebRTC.Models;
using SIPSorceryMedia.Abstractions;
using SIPSorcery.Media;
using SIPSorcery.SIP.App;
using SIPSorcery.Net;
using SIPSorcery.SIP;
using System.Net;
using System.Collections.Generic;

namespace demo;

class Program
{
    static async Task Main()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug() 
            //.MinimumLevel.Verbose()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        var loggerFactory = new SerilogLoggerFactory(Log.Logger);
        SIPSorcery.LogFactory.Set(loggerFactory);

        Log.Logger.Information("WebRTC OpenAI SIP Demo Program");

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY");
        if (string.IsNullOrWhiteSpace(openAiKey))
        {
            Log.Logger.Error("Please provide your OpenAI key as an environment variable. For example: set OPENAI_API_KEY=<your openai api key>");
            return;
        }

        var logger = loggerFactory.CreateLogger<Program>();

        // Set up SIP transport and user agent to receive calls.
        var sipTransport = new SIPTransport();
        var sipUserAgent = new SIPUserAgent(sipTransport, null, true);

        // Listen on UDP 5060 on all interfaces.
        var sipChannel = new SIPUDPChannel(new IPEndPoint(IPAddress.Any, 5060));
        sipTransport.AddSIPChannel(sipChannel);

        Log.Logger.Information("SIP server listening on udp:0.0.0.0:5060");

        sipUserAgent.OnIncomingCall += async (ua, req) =>
        {
            Log.Logger.Information($"Incoming SIP call from: {req.Header.From.FriendlyDescription()} to: {req.URI.User}");

            // Set up the OpenAI WebRTC endpoint.
            var webrtcEndPoint = new WebRTCEndPoint(openAiKey, logger);
            var audioBridge = new RtpToWebRTCAudioBridge();
            webrtcEndPoint.ConnectAudioEndPoint(audioBridge);

            var negotiateConnectResult = await webrtcEndPoint.StartConnect();
            if (negotiateConnectResult.IsLeft)
            {
                Log.Logger.Error($"Failed to negotiate connection to OpenAI Realtime WebRTC endpoint: {negotiateConnectResult.LeftAsEnumerable().First()}");
                return;
            }

            // Set up the SIP RTP media session and bridge it to the WebRTC endpoint.
            var rtpSession = new RTPSession(false, false, false);
            audioBridge.SetRtpSession(rtpSession);

            // Answer the SIP call and start the RTP session.
            var uas = ua.AcceptCall(req);
            bool answerResult = await ua.Answer(uas, rtpSession);
            if (!answerResult)
            {
                Log.Logger.Error("Failed to answer SIP call.");
                return;
            }

            Log.Logger.Information("SIP call answered and audio bridge established.");

            // Optionally, send a session update or trigger conversation on OpenAI side.
            webrtcEndPoint.OnPeerConnectionConnected += () =>
            {
                Log.Logger.Information("WebRTC peer connection established.");
                var voice = RealtimeVoicesEnum.shimmer;
                var sessionUpdateResult = webrtcEndPoint.DataChannelMessenger.SendSessionUpdate(
                    voice,
                    "Keep it short.",
                    transcriptionModel: TranscriptionModelEnum.Whisper1);
                if (sessionUpdateResult.IsLeft)
                {
                    Log.Logger.Error($"Failed to send session update message: {sessionUpdateResult.LeftAsEnumerable().First()}");
                }
                var result = webrtcEndPoint.DataChannelMessenger.SendResponseCreate(voice, "Say Hi!");
                if (result.IsLeft)
                {
                    Log.Logger.Error($"Failed to send response create message: {result.LeftAsEnumerable().First()}");
                }
            };

            webrtcEndPoint.OnDataChannelMessage += (dc, message) =>
            {
                var log = message switch
                {
                    RealtimeServerEventSessionUpdated sessionUpdated => $"Session updated: {sessionUpdated.ToJson()}",
                    RealtimeServerEventConversationItemInputAudioTranscriptionCompleted inputTranscript => $"ME ✅: {inputTranscript.Transcript?.Trim()}",
                    RealtimeServerEventResponseAudioTranscriptDone responseTranscript => $"AI ✅: {responseTranscript.Transcript?.Trim()}",
                    _ => string.Empty
                };
                if (log != string.Empty)
                {
                    Log.Information(log);
                }
            };
        };

        Console.WriteLine("Wait for ctrl-c to indicate user exit.");
        var exitTcs = new TaskCompletionSource<object?>();
        Console.CancelKeyPress += (s, e) =>
        {
            e.Cancel = true;
            exitTcs.TrySetResult(null);
        };
        await exitTcs.Task;
    }
}

// This class bridges audio between the SIP RTP session and the OpenAI WebRTC endpoint.
public class RtpToWebRTCAudioBridge : IAudioEndPoint
{
    private RTPSession? _rtpSession;
    public event EncodedSampleDelegate? OnAudioSourceEncodedSample;
    public event Action<EncodedAudioFrame>? OnAudioSourceEncodedFrameReady;
    public event RawAudioSampleDelegate? OnAudioSourceRawSample { add { } remove { } }
    public event SourceErrorDelegate? OnAudioSourceError;
    public event SourceErrorDelegate? OnAudioSinkError;

    public void SetRtpSession(RTPSession rtpSession)
    {
        _rtpSession = rtpSession;
        // If you want to forward RTP packets to WebRTC, implement this handler as needed.
        // _rtpSession.OnRtpPacketReceived += OnRtpPacketReceived;
    }

    // IAudioSource implementation
    public Task StartAudio() => Task.CompletedTask;
    public Task CloseAudio() => Task.CompletedTask;
    public Task PauseAudio() => Task.CompletedTask;
    public Task ResumeAudio() => Task.CompletedTask;
    public void RestrictFormats(Func<AudioFormat, bool> filter) { }
    public List<AudioFormat> GetAudioSourceFormats() => new List<AudioFormat>();
    public void SetAudioSourceFormat(AudioFormat audioFormat) { }
    public bool HasEncodedAudioSubscribers() => false;
    public bool IsAudioSourcePaused() => false;
    public void ExternalAudioSourceRawSample(AudioSamplingRatesEnum samplingRate, uint durationMilliseconds, short[] sample) { }

    // IAudioSink implementation
    public List<AudioFormat> GetAudioSinkFormats() => new List<AudioFormat>();
    public void SetAudioSinkFormat(AudioFormat audioFormat) { }
    public void GotAudioRtp(System.Net.IPEndPoint remoteEndPoint, uint timestamp, uint ssrc, uint sequenceNumber, int payloadType, bool markerBit, byte[] payload)
    {
        // Forward RTP audio from SIP to WebRTC (if needed)
    }
    public void GotEncodedMediaFrame(EncodedAudioFrame frame) {
        // Forward encoded audio from WebRTC to SIP RTP
        if (_rtpSession != null && frame.EncodedSample != null)
        {
            _rtpSession.SendAudio(0, frame.EncodedSample); // Use 0 for timestamp if not available
        }
    }
    public Task StartAudioSink() => Task.CompletedTask;
    public Task CloseAudioSink() => Task.CompletedTask;
    public Task PauseAudioSink() => Task.CompletedTask;
    public Task ResumeAudioSink() => Task.CompletedTask;

    // IAudioEndPoint implementation
    public Task Start() => Task.CompletedTask;
    public Task Pause() => Task.CompletedTask;
    public Task Resume() => Task.CompletedTask;
    public Task Close() => Task.CompletedTask;
}
