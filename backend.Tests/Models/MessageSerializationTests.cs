using System.Text.Json;
using AgentApp.Backend.Models;
using FluentAssertions;

namespace AgentApp.Backend.Tests.Models;

public class MessageSerializationTests
{
    // Match ASP.NET Core controller JSON config
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    // ── MessagePart polymorphism ──────────────────────────────────────────────

    [Fact]
    public void TextPart_RoundTrips()
    {
        var message = new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Content = "hello" }]
        };

        var json = JsonSerializer.Serialize(message, Options);
        var doc = JsonDocument.Parse(json);
        var part = doc.RootElement.GetProperty("parts")[0];

        part.GetProperty("kind").GetString().Should().Be("text");
        part.GetProperty("content").GetString().Should().Be("hello");

        // Round-trip
        var deserialized = JsonSerializer.Deserialize<Message>(json, Options)!;
        deserialized.Parts.Should().ContainSingle()
            .Which.Should().BeOfType<TextPart>()
            .Which.Content.Should().Be("hello");
    }

    [Fact]
    public void ToolCallPart_RoundTrips()
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Parts =
            [
                new ToolCallPart
                {
                    ToolCall = new ToolCallInfo
                    {
                        ToolName = "ls",
                        Arguments = new Dictionary<string, object?> { ["path"] = "." },
                        Result = "[{\"name\":\"file.txt\"}]",
                        DurationMs = 42
                    }
                }
            ]
        };

        var json = JsonSerializer.Serialize(message, Options);
        var doc = JsonDocument.Parse(json);
        var part = doc.RootElement.GetProperty("parts")[0];

        part.GetProperty("kind").GetString().Should().Be("tool_call");
        part.GetProperty("toolCall").GetProperty("toolName").GetString().Should().Be("ls");
        part.GetProperty("toolCall").GetProperty("durationMs").GetInt64().Should().Be(42);

        // Round-trip
        var deserialized = JsonSerializer.Deserialize<Message>(json, Options)!;
        var tc = deserialized.Parts.Should().ContainSingle()
            .Which.Should().BeOfType<ToolCallPart>().Subject;
        tc.ToolCall.ToolName.Should().Be("ls");
        tc.ToolCall.Result.Should().Be("[{\"name\":\"file.txt\"}]");
        tc.ToolCall.DurationMs.Should().Be(42);
    }

    [Fact]
    public void ContentBlockPart_RoundTrips()
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Parts = [new ContentBlockPart { Source = "report.md", Content = "# Hello" }]
        };

        var json = JsonSerializer.Serialize(message, Options);
        var doc = JsonDocument.Parse(json);
        var part = doc.RootElement.GetProperty("parts")[0];

        part.GetProperty("kind").GetString().Should().Be("content_block");
        part.GetProperty("source").GetString().Should().Be("report.md");
        part.GetProperty("content").GetString().Should().Be("# Hello");

        // Round-trip
        var deserialized = JsonSerializer.Deserialize<Message>(json, Options)!;
        var cb = deserialized.Parts.Should().ContainSingle()
            .Which.Should().BeOfType<ContentBlockPart>().Subject;
        cb.Source.Should().Be("report.md");
        cb.Content.Should().Be("# Hello");
    }

    [Fact]
    public void MixedParts_PreserveOrder()
    {
        var message = new Message
        {
            Role = MessageRole.Assistant,
            Parts =
            [
                new TextPart { Content = "Let me check..." },
                new ToolCallPart
                {
                    ToolCall = new ToolCallInfo { ToolName = "ls", Arguments = [] }
                },
                new ContentBlockPart { Source = "out.md", Content = "data" },
                new TextPart { Content = "Done." }
            ]
        };

        var json = JsonSerializer.Serialize(message, Options);
        var deserialized = JsonSerializer.Deserialize<Message>(json, Options)!;

        deserialized.Parts.Should().HaveCount(4);
        deserialized.Parts[0].Should().BeOfType<TextPart>();
        deserialized.Parts[1].Should().BeOfType<ToolCallPart>();
        deserialized.Parts[2].Should().BeOfType<ContentBlockPart>();
        deserialized.Parts[3].Should().BeOfType<TextPart>();
    }

    [Fact]
    public void MessageRole_SerializesAsString()
    {
        var message = new Message { Role = MessageRole.User, Parts = [] };

        var json = JsonSerializer.Serialize(message, Options);

        json.Should().Contain("\"role\":\"User\"");
        json.Should().NotContain("\"role\":0");
    }

    // ── ClientMessage polymorphism ────────────────────────────────────────────

    [Fact]
    public void SendMessageRequest_RoundTrips()
    {
        ClientMessage msg = new SendMessageRequest("conv1", "hello");

        var json = JsonSerializer.Serialize(msg, Options);
        json.Should().Contain("\"type\":\"send_message\"");

        var deserialized = JsonSerializer.Deserialize<ClientMessage>(json, Options);
        deserialized.Should().BeOfType<SendMessageRequest>()
            .Which.ConversationId.Should().Be("conv1");
    }

    [Fact]
    public void CancelRequest_RoundTrips()
    {
        ClientMessage msg = new CancelRequest("conv1");

        var json = JsonSerializer.Serialize(msg, Options);
        json.Should().Contain("\"type\":\"cancel\"");

        var deserialized = JsonSerializer.Deserialize<ClientMessage>(json, Options);
        deserialized.Should().BeOfType<CancelRequest>();
    }

    [Fact]
    public void PingMessage_RoundTrips()
    {
        ClientMessage msg = new PingMessage();

        var json = JsonSerializer.Serialize(msg, Options);
        json.Should().Contain("\"type\":\"ping\"");

        var deserialized = JsonSerializer.Deserialize<ClientMessage>(json, Options);
        deserialized.Should().BeOfType<PingMessage>();
    }

    // ── ServerMessage polymorphism ────────────────────────────────────────────

    [Fact]
    public void TokenMessage_RoundTrips()
    {
        ServerMessage msg = new TokenMessage("c1", "m1", "hello");

        var json = JsonSerializer.Serialize(msg, Options);
        json.Should().Contain("\"type\":\"token\"");

        var deserialized = JsonSerializer.Deserialize<ServerMessage>(json, Options);
        var token = deserialized.Should().BeOfType<TokenMessage>().Subject;
        token.ConversationId.Should().Be("c1");
        token.Delta.Should().Be("hello");
    }

    [Fact]
    public void ToolResultMessage_RoundTrips()
    {
        ServerMessage msg = new ToolResultMessage("c1", "m1", "ls", "result", 42);

        var json = JsonSerializer.Serialize(msg, Options);
        var deserialized = JsonSerializer.Deserialize<ServerMessage>(json, Options);

        var result = deserialized.Should().BeOfType<ToolResultMessage>().Subject;
        result.ToolName.Should().Be("ls");
        result.DurationMs.Should().Be(42);
    }

    [Fact]
    public void ErrorMessage_RoundTrips()
    {
        ServerMessage msg = new ErrorMessage("c1", "agent_error", "boom");

        var json = JsonSerializer.Serialize(msg, Options);
        var deserialized = JsonSerializer.Deserialize<ServerMessage>(json, Options);

        var error = deserialized.Should().BeOfType<ErrorMessage>().Subject;
        error.Code.Should().Be("agent_error");
        error.Message.Should().Be("boom");
    }

    [Fact]
    public void ContentBlockMessage_RoundTrips()
    {
        ServerMessage msg = new ContentBlockMessage("c1", "m1", "file.md", "# Hi");

        var json = JsonSerializer.Serialize(msg, Options);
        var deserialized = JsonSerializer.Deserialize<ServerMessage>(json, Options);

        var cb = deserialized.Should().BeOfType<ContentBlockMessage>().Subject;
        cb.Source.Should().Be("file.md");
        cb.Content.Should().Be("# Hi");
    }

    [Fact]
    public void ConversationUpdatedMessage_RoundTrips()
    {
        var summary = new ConversationSummary("id1", "Title", DateTimeOffset.UtcNow, 5);
        ServerMessage msg = new ConversationUpdatedMessage(summary);

        var json = JsonSerializer.Serialize(msg, Options);
        var deserialized = JsonSerializer.Deserialize<ServerMessage>(json, Options);

        var updated = deserialized.Should().BeOfType<ConversationUpdatedMessage>().Subject;
        updated.Conversation.Id.Should().Be("id1");
        updated.Conversation.MessageCount.Should().Be(5);
    }
}
