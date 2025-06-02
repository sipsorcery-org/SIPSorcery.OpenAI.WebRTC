using System.Text.Json.Serialization;

namespace SIPSorcery.OpenAIWebRTC.Models;

/// <summary>
/// Configuration for noise reduction on input audio.
/// </summary>
public class InputAudioNoiseReduction
{
    /// <summary>
    /// Type of noise reduction: near-field or far-field.
    /// </summary>
    [JsonPropertyName("type")]
    public NoiseReductionTypeEnum? Type { get; set; }
}
