using OpenAI.Realtime;
using System;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace OpenAI.WebRTC;

public static class JsonSerializationHack
{
    public static JsonSerializerOptions JsonSerializationOptions { get; } = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters =
            {
                //new JsonStringEnumConverterFactory(),
                new RealtimeClientEventConverter(),
                new RealtimeServerEventConverter()
            },
        ReferenceHandler = ReferenceHandler.IgnoreCycles
    };
}

internal class RealtimeServerEventConverter : JsonConverter<IServerEvent>
{
    public override void Write(Utf8JsonWriter writer, IServerEvent value, JsonSerializerOptions options)
        => throw new NotImplementedException();

    public override IServerEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var root = JsonDocument.ParseValue(ref reader).RootElement;
        var type = root.GetProperty("type").GetString()!;

        return type switch
        {
            "error" => root.Deserialize<RealtimeEventError>(options),
            _ when type.StartsWith("session") => root.Deserialize<SessionResponse>(options),
            "conversation.created" => root.Deserialize<RealtimeConversationResponse>(options),
            "conversation.item.created" => root.Deserialize<ConversationItemCreatedResponse>(options),
            _ when type.StartsWith("conversation.item.input_audio_transcription") => root.Deserialize<ConversationItemInputAudioTranscriptionResponse>(options),
            "conversation.item.truncated" => root.Deserialize<ConversationItemTruncatedResponse>(options),
            "conversation.item.deleted" => root.Deserialize<ConversationItemDeletedResponse>(options),
            "input_audio_buffer.committed" => root.Deserialize<InputAudioBufferCommittedResponse>(options),
            "input_audio_buffer.cleared" => root.Deserialize<InputAudioBufferClearedResponse>(options),
            "input_audio_buffer.speech_started" => root.Deserialize<InputAudioBufferStartedResponse>(options),
            "input_audio_buffer.speech_stopped" => root.Deserialize<InputAudioBufferStoppedResponse>(options),
            _ when type.StartsWith("response.audio_transcript") => root.Deserialize<ResponseAudioTranscriptResponse>(options),
            _ when type.StartsWith("response.audio") => root.Deserialize<ResponseAudioResponse>(),
            _ when type.StartsWith("response.content_part") => root.Deserialize<ResponseContentPartResponse>(options),
            _ when type.StartsWith("response.function_call_arguments") => root.Deserialize<ResponseFunctionCallArgumentsResponse>(options),
            _ when type.StartsWith("response.output_item") => root.Deserialize<ResponseOutputItemResponse>(options),
            _ when type.StartsWith("response.text") => root.Deserialize<ResponseTextResponse>(options),
            _ when type.StartsWith("response") => root.Deserialize<RealtimeResponse>(options),
            _ when type.StartsWith("rate_limits") => root.Deserialize<RateLimitsResponse>(options),
            _ => throw new NotImplementedException($"Unknown {nameof(IServerEvent)}: {type}")
        };
    }
}

internal class RealtimeClientEventConverter : JsonConverter<IRealtimeEvent>
{
    public override void Write(Utf8JsonWriter writer, IRealtimeEvent value, JsonSerializerOptions options)
        => throw new NotImplementedException();

    public override IRealtimeEvent? Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
    {
        var root = JsonDocument.ParseValue(ref reader).RootElement;
        var type = root.GetProperty("type").GetString();

        return type switch
        {
            "session.update" => root.Deserialize<UpdateSessionRequest>(options),
            "input_audio_buffer.append" => root.Deserialize<InputAudioBufferAppendRequest>(options),
            "input_audio_buffer.commit" => root.Deserialize<InputAudioBufferCommitRequest>(options),
            "input_audio_buffer.clear" => root.Deserialize<InputAudioBufferClearRequest>(options),
            "conversation.item.create" => root.Deserialize<ConversationItemCreateRequest>(options),
            "conversation.item.truncate" => root.Deserialize<ConversationItemTruncateRequest>(options),
            "conversation.item.delete" => root.Deserialize<ConversationItemDeleteRequest>(options),
            "response.create" => root.Deserialize<CreateResponseRequest>(options),
            "response.cancel" => root.Deserialize<ResponseCancelRequest>(options),
            _ => throw new NotImplementedException($"Unknown {nameof(IRealtimeEvent)}: {type}")
        };
    }
}

internal sealed class JsonStringEnumConverterFactory : JsonConverterFactory
{
    public override bool CanConvert(Type typeToConvert)
        => typeToConvert.IsEnum;

    public override JsonConverter CreateConverter(Type typeToConvert, JsonSerializerOptions options)
        => (JsonConverter)Activator.CreateInstance(typeof(JsonStringEnumConverter<>).MakeGenericType(typeToConvert))!;
}