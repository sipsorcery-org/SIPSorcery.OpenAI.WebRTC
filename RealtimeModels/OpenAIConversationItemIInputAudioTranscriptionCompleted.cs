using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SIPSorcery.OpenAI.WebRTC;

public class OpenAIConversationItemIInputAudioTranscriptionCompleted : OpenAIServerEventBase
{
    public const string TypeName = "conversation.item.input_audio_transcription.completed";

    [JsonPropertyName("content_index")]
    public int ContentIndex { get; set; }

    [JsonPropertyName("item_id")]
    public string? ItemID { get; set; }

    [JsonPropertyName("transcript")]
    public string? Transcript { get; set; }

    public override string ToJson()
    {
        return JsonSerializer.Serialize(this, JsonOptions.Default);
    }
}

