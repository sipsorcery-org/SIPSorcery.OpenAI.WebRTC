using System.Text.Json.Serialization;

namespace SIPSorcery.OpenAIWebRTC.Models;

public class RealtimeToolProperty
{
    [JsonPropertyName("type")]
    public required string Type { get; set; }
}