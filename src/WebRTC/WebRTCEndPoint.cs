//-----------------------------------------------------------------------------
// Filename: WebRTCEndPoint.cs
//
// Description: WebRTC end point for the OpenAI Realtime API. This end point is
// used to establish a WebRTC connection with the OpenAI Realtime API and
// send/receive audio and data channel messages.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 18 May 2025  Aaron Clauson   Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License and the additional
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using System;
using System.Linq;
using System.Threading.Tasks;
using LanguageExt;
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;

namespace SIPSorcery.OpenAI.WebRTC;

public class WebRTCEndPoint : IWebRTCEndPoint, IDisposable
{
    public const string OPENAI_DEFAULT_MODEL = "gpt-4o-realtime-preview-2024-12-17";
    public const string OPENAI_DATACHANNEL_NAME = "oai-events";

    private ILogger _logger = NullLogger.Instance;

    private readonly IWebRTCRestClient _openAIRealtimeRestClient;

    private bool _disposed = false;

    public RTCPeerConnection? PeerConnection { get; private set; }

    public DataChannelMessenger DataChannelMessenger { get; private set; }

    /// <summary>
    /// Event for receiving an encoded media frame from the remote party. Encoded in this
    /// case refers to media encoding, e.g. for audio PCMU, OPUS etc. Currently the OpenAI 
    /// Realtime API only supports audio media frames so the encoded media frames will always
    /// be OPUS encoded audio frames.
    /// </summary>
    public event Action<EncodedAudioFrame>? OnAudioFrameReceived;

    public event Action? OnPeerConnectionConnected;

    public event Action? OnPeerConnectionFailed;

    public event Action? OnPeerConnectionClosed;

    /// <summary>
    /// Raised whenever a parsed OpenAI server event arrives on the data channel.
    /// </summary>
    public event Action<RTCDataChannel, OpenAIServerEventBase>? OnDataChannelMessage;

    /// <summary>
    /// Preferred constructor for dependency injection.
    /// </summary>
    /// <param name="logger"></param>
    /// <param name="openAIRealtimeRestClient"></param>
    public WebRTCEndPoint(
        ILogger<WebRTCEndPoint> logger,
        ILogger<DataChannelMessenger> dataChannelMessengerLogger,
        IWebRTCRestClient openAIRealtimeRestClient)
    {
        _logger = logger;
        _openAIRealtimeRestClient = openAIRealtimeRestClient;

        DataChannelMessenger = new DataChannelMessenger(this, dataChannelMessengerLogger);
    }

    /// <summary>
    /// Constructor for use when not using dependency injection.
    /// </summary>
    /// <param name="openAiKey">The OpenAI bearer token API key.</param>
    /// <param name="logger">Optional logger to use for the end point.</param>
    public WebRTCEndPoint(string openAiKey, ILogger? logger = null)
    {
        var openAIHttpClientFactory = new HttpClientFactory(openAiKey);
        _openAIRealtimeRestClient = new WebRTCRestClient(openAIHttpClientFactory);

        if (logger != null)
        {
            _logger = logger;
        }

        DataChannelMessenger = new DataChannelMessenger(this, logger);
    }

    public async Task<Either<Error, Unit>> StartConnect(RTCConfiguration? pcConfig = null, string? model = null)
    {
        if (PeerConnection != null)
        {
            return Unit.Default;
        }

        PeerConnection = CreatePeerConnection(pcConfig);

        var useModel = string.IsNullOrWhiteSpace(model) ? OPENAI_DEFAULT_MODEL : model;

        var offer = PeerConnection.createOffer();
        await PeerConnection.setLocalDescription(offer).ConfigureAwait(false);

        var sdpAnswerResult = await _openAIRealtimeRestClient.GetSdpAnswerAsync(offer.sdp, useModel).ConfigureAwait(false);

        return sdpAnswerResult.Map(sdpAnswer =>
        {
            var answer = new RTCSessionDescriptionInit
            {
                type = RTCSdpType.answer,
                sdp  = sdpAnswer
            };
            PeerConnection.setRemoteDescription(answer);
            return Unit.Default;
        });
    }

    private RTCPeerConnection CreatePeerConnection(RTCConfiguration? pcConfig)
    {
        PeerConnection = new RTCPeerConnection(pcConfig);

        MediaStreamTrack audioTrack = new MediaStreamTrack(AudioCommonlyUsedFormats.OpusWebRTC, MediaStreamStatusEnum.SendRecv);
        PeerConnection.addTrack(audioTrack);

        // This call is synchronous when the WebRTC connection is not yet connected.
        var dataChannel = PeerConnection.createDataChannel(OPENAI_DATACHANNEL_NAME).Result;

        PeerConnection.onconnectionstatechange += state => _logger.LogDebug($"Peer connection connected changed to {state}.");
        PeerConnection.OnTimeout += mediaType => _logger.LogDebug($"Timeout on media {mediaType}.");
        PeerConnection.oniceconnectionstatechange += state => _logger.LogDebug($"ICE connection state changed to {state}.");

        PeerConnection.onsignalingstatechange += () =>
        {
            if (PeerConnection.signalingState == RTCSignalingState.have_local_offer)
            {
                _logger.LogTrace($"Local SDP:\n{PeerConnection.localDescription.sdp}");
            }
            else if (PeerConnection.signalingState is RTCSignalingState.have_remote_offer or RTCSignalingState.stable)
            {
                _logger.LogTrace($"Remote SDP:\n{PeerConnection.remoteDescription?.sdp}");
            }
        };

        PeerConnection.OnAudioFrameReceived += (encodedAudioFrame) => OnAudioFrameReceived?.Invoke(encodedAudioFrame);

        PeerConnection.onconnectionstatechange += (state) =>
        {
            if (state is RTCPeerConnectionState.failed)
            {
                OnPeerConnectionFailed?.Invoke();
            }
            else if (state is RTCPeerConnectionState.closed or
                RTCPeerConnectionState.disconnected)
            {
                OnPeerConnectionClosed?.Invoke();
            }
        };

        dataChannel.onopen += () => OnPeerConnectionConnected?.Invoke();
        dataChannel.onmessage += DataChannelMessenger.HandleIncomingData;

        return PeerConnection;
    }

    public void SendAudio(uint durationRtpUnits, byte[] sample)
    {
        if (PeerConnection != null && PeerConnection.connectionState == RTCPeerConnectionState.connected)
        {
            PeerConnection.SendAudio(durationRtpUnits, sample);
        }
    }

    public void SendDataChannelMessage(OpenAIServerEventBase message)
    {
        var dc = PeerConnection?.DataChannels.FirstOrDefault();

        if (dc == null)
        {
            _logger.LogError("No data channel available to send message.");
            return;
        }
        else
        {
            _logger.LogDebug($"Sending initial response create to first call data channel {dc.label}.");
            _logger.LogTrace(message.ToJson());

            dc.send(message.ToJson());
        }
    }

    internal void InvokeOnDataChannelMessage(RTCDataChannel dc, OpenAIServerEventBase message)
        => OnDataChannelMessage?.Invoke(dc, message);

    /// <summary>
    /// Closes the PeerConnection and data channel (if open) and raises OnPeerConnectionClosed.
    /// </summary>
    public void Close()
    {
        if (_disposed)
        {
            return;
        }

        if (PeerConnection != null)
        {
            _logger.LogDebug("Closing PeerConnection.");

            PeerConnection.Close("normal");

            OnPeerConnectionClosed?.Invoke();

            PeerConnection.OnAudioFrameReceived -= OnAudioFrameReceived;
        }
    }

    /// <summary>
    /// Disposes the endpoint, closing any active PeerConnection.
    /// </summary>
    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;

        Close();

        GC.SuppressFinalize(this);
    }
}
