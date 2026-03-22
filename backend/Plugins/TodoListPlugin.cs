using System.Collections.Concurrent;
using System.ComponentModel;
using System.Text.Json;
using Microsoft.SemanticKernel;

namespace AgentApp.Backend.Plugins;

public class TodoListPlugin
{
    private readonly ConcurrentDictionary<string, TodoItem> _todos = new();

    [KernelFunction("add_todo")]
    [Description("Add a new todo item to the list.")]
    public string AddTodo(
        [Description("Title of the todo item")] string title,
        [Description("Optional description with more details")] string? description = null)
    {
        var item = new TodoItem
        {
            Id = Guid.NewGuid().ToString(),
            Title = title,
            Description = description ?? "",
            IsComplete = false,
            CreatedAt = DateTimeOffset.UtcNow
        };
        _todos[item.Id] = item;
        return JsonSerializer.Serialize(item);
    }

    [KernelFunction("list_todos")]
    [Description("List all todo items, optionally filtered by status. Use status 'all' (default), 'pending', or 'completed'.")]
    public string ListTodos(
        [Description("Filter: 'all' (default), 'pending', or 'completed'")] string status = "all")
    {
        IEnumerable<TodoItem> items = _todos.Values.OrderBy(t => t.CreatedAt);

        items = status.ToLowerInvariant() switch
        {
            "pending" => items.Where(t => !t.IsComplete),
            "completed" => items.Where(t => t.IsComplete),
            _ => items
        };

        return JsonSerializer.Serialize(items.ToList());
    }

    [KernelFunction("update_todo")]
    [Description("Update the title or description of an existing todo item.")]
    public string UpdateTodo(
        [Description("ID of the todo item to update")] string id,
        [Description("New title (optional)")] string? title = null,
        [Description("New description (optional)")] string? description = null)
    {
        if (!_todos.TryGetValue(id, out var item))
            return $"Todo not found: {id}";

        if (title is not null) item.Title = title;
        if (description is not null) item.Description = description;

        return JsonSerializer.Serialize(item);
    }

    [KernelFunction("complete_todo")]
    [Description("Mark a todo item as complete.")]
    public string CompleteTodo(
        [Description("ID of the todo item to complete")] string id)
    {
        if (!_todos.TryGetValue(id, out var item))
            return $"Todo not found: {id}";

        item.IsComplete = true;
        return $"Marked '{item.Title}' as complete.";
    }

    [KernelFunction("remove_todo")]
    [Description("Remove a todo item from the list.")]
    public string RemoveTodo(
        [Description("ID of the todo item to remove")] string id)
    {
        if (!_todos.TryRemove(id, out var item))
            return $"Todo not found: {id}";

        return $"Removed '{item.Title}'.";
    }

    public class TodoItem
    {
        public string Id { get; set; } = "";
        public string Title { get; set; } = "";
        public string Description { get; set; } = "";
        public bool IsComplete { get; set; }
        public DateTimeOffset CreatedAt { get; set; }
    }
}
