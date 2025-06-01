//-----------------------------------------------------------------------------
// Filename: DataChannelMessenger.cs
//
// Description: Manages messages to control or intiate actions on the OpenAI
// WebRTC session via data channel messages.
//
// Author(s):
// Aaron Clauson (aaron@sipsorcery.com)
// 
// History:
// 31 May 2025  Aaron Clauson   Created, Dublin, Ireland.
//
// License: 
// BSD 3-Clause "New" or "Revised" License and the additional
// BDS BY-NC-SA restriction, see included LICENSE.md file.
//-----------------------------------------------------------------------------

using LanguageExt;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using OpenAI.Realtime;
using SIPSorcery.Net;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace OpenAI.WebRTC;

/// <summary>
/// Facilitates sending OpenAI control messages (session updates, response requests, etc.) 
/// over the established WebRTC data channel to manage and orchestrate the OpenAI-powered media session.
/// </summary>
public class DataChannelMessenger
{
    private readonly WebRTCEndPoint _endpoint;
    private readonly ILogger _logger = NullLogger.Instance;

    public DataChannelMessenger(
        WebRTCEndPoint endpoint,
        ILogger<DataChannelMessenger> logger)
    {
        _endpoint = endpoint;
        _logger = logger ?? _logger;
    }

    public DataChannelMessenger(
        WebRTCEndPoint endpoint,
        ILogger? logger)
    {
        _endpoint = endpoint;
        _logger = logger ?? _logger;
    }

    /// <summary>
    /// Sends an OpenAI session‐update event over the data channel.
    /// </summary>
    public Either<LanguageExt.Common.Error, Unit> SendSessionUpdate(
        OpenAIVoicesEnum voice,
        bool useInputAudioTranscription = false,
        string? instructions = null,
        string? model = null)
    {
        // Validate PeerConnection and retrieve the first data channel
        var dcResult = ValidateDataChannel();
        if (dcResult.IsLeft)
        {
            return dcResult.LeftToList().First();
        }

        var dc = dcResult.RightToList().First();

        var message = new Realtime.UpdateSessionRequest(
            new Realtime.SessionConfiguration(
                Models.Model.GPT4oRealtime, 
                voice: Voice.Coral, 
                transcriptionModel: useInputAudioTranscription ? Models.Model.Whisper1 : null));

        _logger.LogDebug(
            "Sending session‐update message on data channel “{Label}”: {Json}",
            dc.label,
            JsonSerializer.Serialize(message, JsonSerializationHack.JsonSerializationOptions));

        dc.send(JsonSerializer.Serialize(message, JsonSerializationHack.JsonSerializationOptions));
        return Unit.Default;
    }

    /// <summary>
    /// Sends an OpenAI response‐create event over the data channel.
    /// </summary>
    public Either<LanguageExt.Common.Error, Unit> SendResponseCreate(
        OpenAIVoicesEnum voice,
        string instructions)
    {
        // Validate PeerConnection and retrieve the first data channel
        var dcResult = ValidateDataChannel();
        if (dcResult.IsLeft)
        {
            return dcResult.LeftToList().First();
        }

        var dc = dcResult.RightToList().First();

        var message = new Realtime.CreateResponseRequest(
            new RealtimeResponseCreateParams(Modality.Text | Modality.Audio, instructions, voice.ToString()));

        _logger.LogTrace(
            "Sending response‐create message on data channel “{Label}”: {Json}",
            dc.label,
            JsonSerializer.Serialize(message, JsonSerializationHack.JsonSerializationOptions));

        dc.send(JsonSerializer.Serialize(message, JsonSerializationHack.JsonSerializationOptions));
        return Unit.Default;
    }

    /// <summary>
    /// Handles any incoming raw data from the WebRTC data channel. Parses the JSON,
    /// turns it into the appropriate subtype, and
    /// then invokes the endpoint's <see cref="IWebRTCEndPoint.OnDataChannelMessage"/> event.
    /// </summary>
    public void HandleIncomingData(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        string msgText = Encoding.UTF8.GetString(data);

        try
        {
            var parsedEvent = JsonSerializer.Deserialize<IServerEvent>(msgText, JsonSerializationHack.JsonSerializationOptions);

            if (parsedEvent != null)
            {
                _endpoint.InvokeOnDataChannelMessage(dc, parsedEvent);
            }
            else
            {
                _logger.LogWarning("Unexpected event type '{Type}' received on OpenAI data channel.", parsedEvent?.Type);
            }
        }
        catch(Exception)
        {
            _logger.LogWarning($"JSON parse error on OpenAI data channel for {msgText}");
        }
    }

    /// <summary>
    /// Validates that the PeerConnection is non-null and connected, and that at least one data channel exists.
    /// Returns Either an Error describing the failure, or the first RTCDataChannel.
    /// </summary>
    private Either<LanguageExt.Common.Error, RTCDataChannel> ValidateDataChannel()
    {
        return _endpoint.PeerConnection.Match<Either<LanguageExt.Common.Error, RTCDataChannel>>(
            pc =>
            {
                if (pc.connectionState != RTCPeerConnectionState.connected)
                {
                    return LanguageExt.Common.Error.New("Peer connection not connected.");
                }

                var dc = pc.DataChannels
                            .FirstOrDefault(x => x.label == WebRTCEndPoint.OPENAI_DATACHANNEL_NAME);
                if (dc == null)
                {
                    return LanguageExt.Common.Error.New("No OpenAI data channel available.");
                }

                return dc;
            },
            () => LanguageExt.Common.Error.New("Peer connection not established.")
        );
    }
}
