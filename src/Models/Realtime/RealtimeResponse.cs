﻿using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace SIPSorcery.OpenAIWebRTC.Models;

/// <summary>
/// Represents a response generated by the realtime model.
/// Includes metadata, output items, usage statistics, and configuration.
/// </summary>
public class RealtimeResponse
{
    [JsonPropertyName("id")]
    public string? ID { get; set; }

    [JsonPropertyName("object")]
    public string Object { get; set; } = "realtime.response";

    [JsonPropertyName("status")]
    public RealtimeStatusEnum? Status { get; set; }

    [JsonPropertyName("status_details")]
    public RealtimeResponseStatusDetails? StatusDetails { get; set; }

    [JsonPropertyName("output")]
    public List<RealtimeConversationItem>? Output { get; set; }

    [JsonPropertyName("metadata")]
    public Metadata? Metadata { get; set; }

    [JsonPropertyName("usage")]
    public RealtimeResponseUsage? Usage { get; set; }

    [JsonPropertyName("conversation_id")]
    public string? ConversationID { get; set; }

    [JsonPropertyName("voice")]
    public string? Voice { get; set; }

    [JsonPropertyName("modalities")]
    public List<RealtimeModalityEnum>? Modalities { get; set; }

    [JsonPropertyName("output_audio_format")]
    public RealtimeAudioFormatEnum? OutputAudioFormat { get; set; }

    [JsonPropertyName("temperature")]
    public float? Temperature { get; set; }

    [JsonPropertyName("max_output_tokens")]
    public string? MaxOutputTokens { get; set; }
}
