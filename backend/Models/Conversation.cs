using Microsoft.SemanticKernel.Agents;

namespace AgentApp.Backend.Models;

public class Conversation(string id)
{
    public string Id { get; } = id;
    public string Title { get; set; } = "New conversation";
    public DateTimeOffset CreatedAt { get; } = DateTimeOffset.UtcNow;

    // SK manages chat history internally in the thread
    public ChatHistoryAgentThread Thread { get; } = new();

    // UI-facing message log
    public List<Message> Messages { get; } = [];

    public ConversationSummary ToSummary() =>
        new(Id, Title, CreatedAt, Messages.Count);
}

public record ConversationSummary(
    string Id,
    string Title,
    DateTimeOffset CreatedAt,
    int MessageCount);
