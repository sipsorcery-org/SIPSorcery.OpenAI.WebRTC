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
using LanguageExt.Common;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using SIPSorcery.Net;
using System;
using System.Linq;
using System.Text;
using System.Text.Json;

namespace SIPSorcery.OpenAI.WebRTC;

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
    public Either<Error, Unit> SendSessionUpdate(
        OpenAIVoicesEnum voice,
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

        var message = new OpenAISessionUpdate
        {
            EventID = Guid.NewGuid().ToString(),
            Session = new OpenAISession
            {
                Voice = voice,
                Instructions = instructions
            }
        };

        if (!string.IsNullOrWhiteSpace(model))
        {
            message.Session.Model = model;
        }

        _logger.LogTrace(
            "Sending session‐update message on data channel “{Label}”: {Json}",
            dc.label,
            message.ToJson());

        dc.send(message.ToJson());
        return Unit.Default;
    }

    /// <summary>
    /// Sends an OpenAI response‐create event over the data channel.
    /// </summary>
    public Either<Error, Unit> SendResponseCreate(
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

        var message = new OpenAIResponseCreate
        {
            EventID = Guid.NewGuid().ToString(),
            Response = new OpenAIResponseCreateResponse
            {
                Voice = voice.ToString(),
                Instructions = instructions
            }
        };

        _logger.LogTrace(
            "Sending response‐create message on data channel “{Label}”: {Json}",
            dc.label,
            message.ToJson());

        dc.send(message.ToJson());
        return Unit.Default;
    }

    /// <summary>
    /// Handles any incoming raw data from the WebRTC data channel. Parses the JSON,
    /// turns it into the appropriate <see cref="OpenAIServerEventBase"/> subtype, and
    /// then invokes the endpoint's <see cref="IWebRTCEndPoint.OnDataChannelMessage"/> event.
    /// </summary>
    public void HandleIncomingData(RTCDataChannel dc, DataChannelPayloadProtocols protocol, byte[] data)
    {
        string msgText = Encoding.UTF8.GetString(data);

        // Attempt a base‐type JSON deserialize
        var baseEvent = JsonSerializer.Deserialize<OpenAIServerEventBase>(msgText, JsonOptions.Default);
        if (baseEvent == null)
        {
            _logger.LogWarning("Received non‐OpenAI event on data channel: {Payload}", msgText);
            return;
        }

        // Dispatch into the concrete subtype based on 'Type' field:
        OpenAIServerEventBase? parsedEvent = baseEvent.Type switch
        {
            OpenAIConversationItemCreated.TypeName => JsonSerializer.Deserialize<OpenAIConversationItemCreated>(msgText, JsonOptions.Default),
            OpenAIInputAudioBufferCommitted.TypeName => JsonSerializer.Deserialize<OpenAIInputAudioBufferCommitted>(msgText, JsonOptions.Default),
            OpenAIInputAudioBufferSpeechStarted.TypeName => JsonSerializer.Deserialize<OpenAIInputAudioBufferSpeechStarted>(msgText, JsonOptions.Default),
            OpenAIInputAudioBufferSpeechStopped.TypeName => JsonSerializer.Deserialize<OpenAIInputAudioBufferSpeechStopped>(msgText, JsonOptions.Default),
            OpenAIOuputAudioBufferAudioStarted.TypeName => JsonSerializer.Deserialize<OpenAIOuputAudioBufferAudioStarted>(msgText, JsonOptions.Default),
            OpenAIOuputAudioBufferAudioStopped.TypeName => JsonSerializer.Deserialize<OpenAIOuputAudioBufferAudioStopped>(msgText, JsonOptions.Default),
            OpenAIRateLimitsUpdated.TypeName => JsonSerializer.Deserialize<OpenAIRateLimitsUpdated>(msgText, JsonOptions.Default),
            OpenAIResponseAudioDone.TypeName => JsonSerializer.Deserialize<OpenAIResponseAudioDone>(msgText, JsonOptions.Default),
            OpenAIResponseAudioTranscriptDelta.TypeName => JsonSerializer.Deserialize<OpenAIResponseAudioTranscriptDelta>(msgText, JsonOptions.Default),
            OpenAIResponseAudioTranscriptDone.TypeName => JsonSerializer.Deserialize<OpenAIResponseAudioTranscriptDone>(msgText, JsonOptions.Default),
            OpenAIResponseContentPartAdded.TypeName => JsonSerializer.Deserialize<OpenAIResponseContentPartAdded>(msgText, JsonOptions.Default),
            OpenAIResponseContentPartDone.TypeName => JsonSerializer.Deserialize<OpenAIResponseContentPartDone>(msgText, JsonOptions.Default),
            OpenAIResponseCreated.TypeName => JsonSerializer.Deserialize<OpenAIResponseCreated>(msgText, JsonOptions.Default),
            OpenAIResponseDone.TypeName => JsonSerializer.Deserialize<OpenAIResponseDone>(msgText, JsonOptions.Default),
            OpenAIResponseFunctionCallArgumentsDelta.TypeName => JsonSerializer.Deserialize<OpenAIResponseFunctionCallArgumentsDelta>(msgText, JsonOptions.Default),
            OpenAIResponseFunctionCallArgumentsDone.TypeName => JsonSerializer.Deserialize<OpenAIResponseFunctionCallArgumentsDone>(msgText, JsonOptions.Default),
            OpenAIResponseOutputItemAdded.TypeName => JsonSerializer.Deserialize<OpenAIResponseOutputItemAdded>(msgText, JsonOptions.Default),
            OpenAIResponseOutputItemDone.TypeName => JsonSerializer.Deserialize<OpenAIResponseOutputItemDone>(msgText, JsonOptions.Default),
            OpenAISessionCreated.TypeName => JsonSerializer.Deserialize<OpenAISessionCreated>(msgText, JsonOptions.Default),
            OpenAISessionUpdated.TypeName => JsonSerializer.Deserialize<OpenAISessionUpdated>(msgText, JsonOptions.Default),
            _ => null
        };

        if (parsedEvent != null)
        {
            _endpoint.InvokeOnDataChannelMessage(dc, parsedEvent);
        }
        else
        {
            _logger.LogWarning("Unexpected event type '{Type}' received on OpenAI data channel.", baseEvent.Type);
        }
    }

    /// <summary>
    /// Validates that the PeerConnection is non-null and connected, and that at least one data channel exists.
    /// Returns Either an Error describing the failure, or the first RTCDataChannel.
    /// </summary>
    private Either<Error, RTCDataChannel> ValidateDataChannel()
    {
        return _endpoint.PeerConnection.Match<Either<Error, RTCDataChannel>>(
               pc =>
               {
                   if (pc.connectionState != RTCPeerConnectionState.connected)
                   {
                       return Error.New("Peer connection not connected.");
                   }

                   var dc = pc.DataChannels
                              .FirstOrDefault(x => x.label == WebRTCEndPoint.OPENAI_DATACHANNEL_NAME);
                   if (dc == null)
                   {
                       return Error.New("No OpenAI data channel available.");
                   }

                   return dc;
               },
               () => Error.New("Peer connection not established.")
           );
    }
}
