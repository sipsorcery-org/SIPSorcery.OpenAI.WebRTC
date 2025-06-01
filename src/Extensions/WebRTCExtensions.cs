﻿//-----------------------------------------------------------------------------
// Filename: WebRTCExtensions.cs
//
// Description: Extension method to register OpenAI Realtime WebRTC client
// and required services in the DI container.
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

using LanguageExt;
using Microsoft.Extensions.DependencyInjection;
using SIPSorcery.Net;
using SIPSorceryMedia.Abstractions;
using System;
using System.Net.Http.Headers;

namespace SIPSorcery.OpenAI.WebRTC;

/// <summary>
/// Extension methods to work with the OpenAI Realtime WebRTC end point.
/// </summary>
public static class WebRTCServiceCollectionExtensions
{
    private const int OPENAI_HTTP_CLIENT_TIMEOUT_SECONDS = 5;

    /// <summary>
    /// Adds and configures the OpenAI Realtime REST and WebRTC endpoint clients.
    /// </summary>
    /// <param name="services">The service collection to add registrations to.</param>
    /// <param name="openAiKey">Your OpenAI API key for authorization.</param>
    /// <returns>The original <see cref="IServiceCollection"/> for chaining.</returns>
    public static IServiceCollection AddOpenAIRealtimeWebRTC(this IServiceCollection services, string openAiKey)
    {
        if (string.IsNullOrWhiteSpace(openAiKey))
        {
            throw new ArgumentException("OpenAI API key must be provided", nameof(openAiKey));
        }

        // Register the HTTP client for the REST client
        services
            .AddHttpClient(WebRTCRestClient.OPENAI_HTTP_CLIENT_NAME, client =>
            {
                client.Timeout = TimeSpan.FromSeconds(OPENAI_HTTP_CLIENT_TIMEOUT_SECONDS);
                client.DefaultRequestHeaders.Authorization =
                    new AuthenticationHeaderValue("Bearer", openAiKey);
            });

        // Register the REST and WebRTC clients
        services.AddTransient<IWebRTCRestClient, WebRTCRestClient>();
        services.AddTransient<IWebRTCEndPoint, WebRTCEndPoint>();

        return services;
    }

    /// <summary>
    /// Connects an audio endpoint to a WebRTC end point. The standard use case is to connect the audio from the OpeNAI end point
    /// to local audio devices (speakers and/or microphone).
    /// </summary>
    /// <param name="webRTCEndPoint">The WebRTC end point to connect.</param>
    /// <param name="audioEndPoint">The audio end point to connect.</param>
    public static void ConnectAudioEndPoint(this IWebRTCEndPoint webRTCEndPoint, IAudioEndPoint audioEndPoint)
    {
        audioEndPoint.OnAudioSourceEncodedSample += webRTCEndPoint.SendAudio;
        webRTCEndPoint.OnAudioFrameReceived += audioEndPoint.GotEncodedMediaFrame;

        webRTCEndPoint.OnPeerConnectionConnected += async () =>
        {
            await audioEndPoint.StartAudio();
            await audioEndPoint.StartAudioSink();
            await audioEndPoint.Start();
        };

        webRTCEndPoint.OnPeerConnectionFailed += async () =>
        {
            audioEndPoint.OnAudioSourceEncodedSample -= webRTCEndPoint.SendAudio;
            webRTCEndPoint.OnAudioFrameReceived -= audioEndPoint.GotEncodedMediaFrame;
            await audioEndPoint.Close();
        };

        webRTCEndPoint.OnPeerConnectionClosed += async () =>
        {
            audioEndPoint.OnAudioSourceEncodedSample -= webRTCEndPoint.SendAudio;
            webRTCEndPoint.OnAudioFrameReceived -= audioEndPoint.GotEncodedMediaFrame;
            await audioEndPoint.Close();
        };
    }

    /// <summary>
    /// Pipes encoded audio frames from the <paramref name="sourceEndpoint"/> to the
    /// <paramref name="destinationEndpoint"/> by directly forwarding RTP audio packets.
    /// If either endpoint’s PeerConnection is None, this method does nothing.
    /// </summary>
    /// <param name="sourceEndpoint">The IWebRTCEndPoint to receive audio frames from.</param>
    /// <param name="destinationEndpoint">The IWebRTCEndPoint to send audio frames to.</param>
    public static void PipeAudioTo(this IWebRTCEndPoint sourceEndpoint, IWebRTCEndPoint destinationEndpoint)
    {
        sourceEndpoint.PeerConnection.Match(
            srcPc => destinationEndpoint.PeerConnection.Match(
                destPc =>
                {
                    // Both endpoints have a valid RTCPeerConnection, so wire up audio forwarding:
                    srcPc.PipeAudioTo(destPc);
                    return Unit.Default;
                },
                () => Unit.Default
            ),
            () => Unit.Default
        );
    }

    /// <summary>
    /// Pipes encoded audio frames from the <paramref name="source"/> peer connection
    /// to the <paramref name="destination"/> peer connection by directly forwarding encoded audio 
    /// payloads. This approach will only work where the peer connecctions are using the same
    /// audio encoding.
    /// </summary>
    /// <param name="source">The RTCPeerConnection to receive audio payloads from.</param>
    /// <param name="destination">The RTCPeerConnection to send audio payloads to.</param>
    public static void PipeAudioTo(this RTCPeerConnection source, RTCPeerConnection destination)
    {
        // Pipe audio payloads receied from the source WebRTC peer connection to the destination peer connection.
        source.OnAudioFrameReceived += (encodeAudioFrame) => destination.SendAudio(
            RtpTimestampExtensions.ToRtpUnits(encodeAudioFrame.DurationMilliSeconds, destination.AudioStream.NegotiatedFormat.ToAudioFormat().RtpClockRate),
            encodeAudioFrame.EncodedAudio);
    }
}
