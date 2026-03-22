using System.Collections.Concurrent;
using AgentApp.Backend.Models;

namespace AgentApp.Backend.Services;

public class ConversationStore
{
    private readonly ConcurrentDictionary<string, Conversation> _store = new();

    public Conversation GetOrCreate(string id) =>
        _store.GetOrAdd(id, id => new Conversation(id));

    public Conversation? Get(string id) =>
        _store.TryGetValue(id, out var c) ? c : null;

    public Conversation Create()
    {
        var c = new Conversation(Guid.NewGuid().ToString());
        _store[c.Id] = c;
        return c;
    }

    public bool Delete(string id) => _store.TryRemove(id, out _);

    public IReadOnlyList<ConversationSummary> GetAll() =>
        _store.Values
            .OrderByDescending(c => c.CreatedAt)
            .Select(c => c.ToSummary())
            .ToList();

    public void DeriveTitle(Conversation c)
    {
        if (c.Title != "New conversation") return;
        var first = c.Messages.FirstOrDefault(m => m.Role == MessageRole.User);
        if (first is null) return;
        var text = string.Concat(first.Parts.OfType<TextPart>().Select(p => p.Content));
        c.Title = text.Length > 60 ? text[..57] + "..." : text;
    }
}
