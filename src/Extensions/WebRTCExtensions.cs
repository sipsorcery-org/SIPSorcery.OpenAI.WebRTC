//-----------------------------------------------------------------------------
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

using Microsoft.Extensions.DependencyInjection;
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
}
