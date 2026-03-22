using System.Text.Json.Serialization;

namespace AgentApp.Backend.Models;

// ── Client → Server ──────────────────────────────────────────────────────────

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(SendMessageRequest), "send_message")]
[JsonDerivedType(typeof(CancelRequest), "cancel")]
[JsonDerivedType(typeof(PingMessage), "ping")]
public abstract record ClientMessage;

public record SendMessageRequest(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("content")] string Content) : ClientMessage;

public record CancelRequest(
    [property: JsonPropertyName("conversationId")] string ConversationId) : ClientMessage;

public record PingMessage : ClientMessage;

// ── Server → Client ──────────────────────────────────────────────────────────

[JsonPolymorphic(TypeDiscriminatorPropertyName = "type")]
[JsonDerivedType(typeof(AgentStartMessage), "agent_start")]
[JsonDerivedType(typeof(TokenMessage), "token")]
[JsonDerivedType(typeof(ToolCallMessage), "tool_call")]
[JsonDerivedType(typeof(ToolResultMessage), "tool_result")]
[JsonDerivedType(typeof(AgentDoneMessage), "agent_done")]
[JsonDerivedType(typeof(ErrorMessage), "error")]
[JsonDerivedType(typeof(PongMessage), "pong")]
[JsonDerivedType(typeof(ConversationUpdatedMessage), "conversation_updated")]
[JsonDerivedType(typeof(ContentBlockMessage), "content_block")]
public abstract record ServerMessage;

public record AgentStartMessage(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("messageId")] string MessageId) : ServerMessage;

public record TokenMessage(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("delta")] string Delta) : ServerMessage;

public record ToolCallMessage(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("toolName")] string ToolName,
    [property: JsonPropertyName("arguments")] Dictionary<string, object?> Arguments) : ServerMessage;

public record ToolResultMessage(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("toolName")] string ToolName,
    [property: JsonPropertyName("result")] string Result,
    [property: JsonPropertyName("durationMs")] long DurationMs) : ServerMessage;

public record AgentDoneMessage(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("messageId")] string MessageId) : ServerMessage;

public record ErrorMessage(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("code")] string Code,
    [property: JsonPropertyName("message")] string Message) : ServerMessage;

public record PongMessage : ServerMessage;

public record ConversationUpdatedMessage(
    [property: JsonPropertyName("conversation")] ConversationSummary Conversation) : ServerMessage;

public record ContentBlockMessage(
    [property: JsonPropertyName("conversationId")] string ConversationId,
    [property: JsonPropertyName("messageId")] string MessageId,
    [property: JsonPropertyName("source")] string Source,
    [property: JsonPropertyName("content")] string Content) : ServerMessage;
