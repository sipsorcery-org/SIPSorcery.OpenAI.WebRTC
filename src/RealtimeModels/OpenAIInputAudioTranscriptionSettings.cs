using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SIPSorcery.OpenAI.WebRTC;

public class OpenAIInputAudioTranscriptionSettings
{
    [JsonPropertyName("model")]
    public string Model { get; set; } = "whisper-1";
}

