﻿//-----------------------------------------------------------------------------
// Filename: Program.cs
//
// Description: An example WebRTC application that can be used to interact with
// OpenAI's real-time API https://platform.openai.com/docs/guides/realtime-webrtc.
//
// NOTE: As of 10 May 2025 this example does work to establish an audio stream and is
// able to receive data channel messages. There is no echo cancellation feature in this
// demo so if not provided by the by your Windows audio device then ChatGPT will end
// up talking to itself (as a workaround use a headset).
//
// Usage:
// set OPENAI_API_KEY=your_openai_key
// dotnet run
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 19 Dec 2024	Aaron Clauson	Created, Dublin, Ireland.
// 17 Jan 2025  Aaron Clauson   Added create resposne data channel message to trigger conversation start.
// 10 May 2025  Aaron Clauson   Big refactor of the OpenAI.Realtime library to use HttpClientFactory.
// 27 May 2025  Aaron Clauson   Moved from SIPSorcery main repo to SIPSorcery.OpenAI.WebRTC repo.
//
// License: 
// BSD 3-Clause "New" or "Revised" License and the additional
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Threading.Tasks;
using Serilog;
using SIPSorceryMedia.Windows;
using Microsoft.Extensions.DependencyInjection;
using Serilog.Extensions.Logging;
using SIPSorcery.OpenAIWebRTC;
using SIPSorcery.OpenAIWebRTC.Models;
using SIPSorceryMedia.Abstractions;
using SIPSorcery.Media;

namespace demo;

class Program
{
    static async Task Main()
    {
        Log.Logger = new LoggerConfiguration()
            .MinimumLevel.Debug()
            .Enrich.FromLogContext()
            .WriteTo.Console()
            .CreateLogger();

        var factory = new SerilogLoggerFactory(Log.Logger);
        SIPSorcery.LogFactory.Set(factory);

        Log.Logger.Information("WebRTC OpenAI Demo Program");

        var openAiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") ?? string.Empty;

        if (string.IsNullOrWhiteSpace(openAiKey))
        {
            Log.Logger.Error("Please provide your OpenAI key as an environment variable. For example: set OPENAI_API_KEY=<your openai api key>");
            return;
        }

        // Set up DI.
        var services = new ServiceCollection();

        services.AddLogging(builder =>
        {
            builder.AddSerilog(dispose: true);
        });

        services.AddOpenAIRealtimeWebRTC(openAiKey);

        using var provider = services.BuildServiceProvider();
        var webrtcEndPoint = provider.GetRequiredService<IWebRTCEndPoint>();

        // We'll send/receive audio directly from our Windows audio devices.
        var windowsAudioEp = InitialiseWindowsAudioEndPoint();
        webrtcEndPoint.ConnectAudioEndPoint(windowsAudioEp);

        var negotiateConnectResult = await webrtcEndPoint.StartConnect();

        if (negotiateConnectResult.IsLeft)
        {
            Log.Logger.Error($"Failed to negotiation connection to OpenAI Realtime WebRTC endpoint: {negotiateConnectResult.LeftAsEnumerable().First()}");
            return;
        }

        webrtcEndPoint.OnPeerConnectionConnected += () =>
        {
            Log.Logger.Information("WebRTC peer connection established.");

            // Trigger the conversation by sending a response create message.
            var result = webrtcEndPoint.DataChannelMessenger.SendResponseCreate(RealtimeVoicesEnum.shimmer, "Say Hi!");
            if (result.IsLeft)
            {
                Log.Logger.Error($"Failed to send response create message: {result.LeftAsEnumerable().First()}");
            }
        };

        webrtcEndPoint.OnDataChannelMessage += (dc, message) =>
        {
            if (message is RealtimeServerEventResponseAudioTranscriptDone done)
            {
                Log.Information($"Transcript done: {done.Transcript}");
            }
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

    private static WindowsAudioEndPoint InitialiseWindowsAudioEndPoint()
    {
        var audioEncoder = new AudioEncoder(AudioCommonlyUsedFormats.OpusWebRTC);
        return new WindowsAudioEndPoint(audioEncoder);
    }
}
