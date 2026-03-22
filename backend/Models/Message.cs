using System.Text.Json.Serialization;

namespace AgentApp.Backend.Models;

public class Message
{
    public string Id { get; init; } = Guid.NewGuid().ToString();

    [JsonConverter(typeof(JsonStringEnumConverter))]
    public MessageRole Role { get; init; }

    public List<MessagePart> Parts { get; init; } = [];
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public enum MessageRole { User, Assistant }

// ── Message part hierarchy ────────────────────────────────────────────────────

[JsonPolymorphic(TypeDiscriminatorPropertyName = "kind")]
[JsonDerivedType(typeof(TextPart), "text")]
[JsonDerivedType(typeof(ToolCallPart), "tool_call")]
[JsonDerivedType(typeof(ContentBlockPart), "content_block")]
[JsonDerivedType(typeof(DataBlockPart), "data_block")]
public abstract class MessagePart { }

public class TextPart : MessagePart
{
    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class ToolCallPart : MessagePart
{
    [JsonPropertyName("toolCall")]
    public ToolCallInfo ToolCall { get; set; } = new();
}

public class ToolCallInfo
{
    [JsonPropertyName("toolName")]
    public string ToolName { get; set; } = "";

    [JsonPropertyName("arguments")]
    public Dictionary<string, object?> Arguments { get; set; } = [];

    [JsonPropertyName("result")]
    public string? Result { get; set; }

    [JsonPropertyName("durationMs")]
    public long? DurationMs { get; set; }
}

public class ContentBlockPart : MessagePart
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("content")]
    public string Content { get; set; } = "";
}

public class DataBlockPart : MessagePart
{
    [JsonPropertyName("source")]
    public string Source { get; set; } = "";

    [JsonPropertyName("columns")]
    public List<DataColumnInfo> Columns { get; set; } = [];

    [JsonPropertyName("rows")]
    public List<List<object?>> Rows { get; set; } = [];

    [JsonPropertyName("totalRowCount")]
    public long TotalRowCount { get; set; }

    [JsonPropertyName("previewRowCount")]
    public int PreviewRowCount { get; set; }
}

public class DataColumnInfo
{
    [JsonPropertyName("name")]
    public string Name { get; set; } = "";

    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}
