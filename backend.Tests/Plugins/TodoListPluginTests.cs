using System.Text.Json;
using AgentApp.Backend.Plugins;
using FluentAssertions;

namespace AgentApp.Backend.Tests.Plugins;

public class TodoListPluginTests
{
    private readonly TodoListPlugin _plugin = new();

    // ── add_todo ──────────────────────────────────────────────────────────────

    [Fact]
    public void AddTodo_CreatesItem()
    {
        var result = _plugin.AddTodo("Buy groceries", "Milk, eggs, bread");
        var item = JsonSerializer.Deserialize<JsonElement>(result);

        item.GetProperty("Id").GetString().Should().NotBeNullOrEmpty();
        item.GetProperty("Title").GetString().Should().Be("Buy groceries");
        item.GetProperty("Description").GetString().Should().Be("Milk, eggs, bread");
        item.GetProperty("IsComplete").GetBoolean().Should().BeFalse();
    }

    [Fact]
    public void AddTodo_WithoutDescription_DefaultsToEmpty()
    {
        var result = _plugin.AddTodo("Simple task");
        var item = JsonSerializer.Deserialize<JsonElement>(result);

        item.GetProperty("Description").GetString().Should().BeEmpty();
    }

    // ── list_todos ────────────────────────────────────────────────────────────

    [Fact]
    public void ListTodos_ReturnsAll()
    {
        _plugin.AddTodo("Task 1");
        _plugin.AddTodo("Task 2");

        var result = _plugin.ListTodos();
        var items = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        items.Should().HaveCount(2);
    }

    [Fact]
    public void ListTodos_Empty_ReturnsEmptyArray()
    {
        var result = _plugin.ListTodos();
        result.Should().Be("[]");
    }

    [Fact]
    public void ListTodos_FiltersPending()
    {
        _plugin.AddTodo("Task 1");
        var addResult = JsonSerializer.Deserialize<JsonElement>(_plugin.AddTodo("Task 2"));
        var id = addResult.GetProperty("Id").GetString()!;
        _plugin.CompleteTodo(id);

        var result = _plugin.ListTodos("pending");
        var items = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        items.Should().HaveCount(1);
        items[0].GetProperty("Title").GetString().Should().Be("Task 1");
    }

    [Fact]
    public void ListTodos_FiltersCompleted()
    {
        _plugin.AddTodo("Task 1");
        var addResult = JsonSerializer.Deserialize<JsonElement>(_plugin.AddTodo("Task 2"));
        var id = addResult.GetProperty("Id").GetString()!;
        _plugin.CompleteTodo(id);

        var result = _plugin.ListTodos("completed");
        var items = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        items.Should().HaveCount(1);
        items[0].GetProperty("Title").GetString().Should().Be("Task 2");
    }

    // ── update_todo ───────────────────────────────────────────────────────────

    [Fact]
    public void UpdateTodo_ChangesTitle()
    {
        var addResult = JsonSerializer.Deserialize<JsonElement>(_plugin.AddTodo("Old title"));
        var id = addResult.GetProperty("Id").GetString()!;

        var result = _plugin.UpdateTodo(id, title: "New title");
        var item = JsonSerializer.Deserialize<JsonElement>(result);

        item.GetProperty("Title").GetString().Should().Be("New title");
    }

    [Fact]
    public void UpdateTodo_ChangesDescription()
    {
        var addResult = JsonSerializer.Deserialize<JsonElement>(_plugin.AddTodo("Task", "Old desc"));
        var id = addResult.GetProperty("Id").GetString()!;

        var result = _plugin.UpdateTodo(id, description: "New desc");
        var item = JsonSerializer.Deserialize<JsonElement>(result);

        item.GetProperty("Description").GetString().Should().Be("New desc");
    }

    [Fact]
    public void UpdateTodo_NotFound_ReturnsMessage()
    {
        var result = _plugin.UpdateTodo("nonexistent");
        result.Should().StartWith("Todo not found:");
    }

    // ── complete_todo ─────────────────────────────────────────────────────────

    [Fact]
    public void CompleteTodo_MarksComplete()
    {
        var addResult = JsonSerializer.Deserialize<JsonElement>(_plugin.AddTodo("My task"));
        var id = addResult.GetProperty("Id").GetString()!;

        var result = _plugin.CompleteTodo(id);

        result.Should().Contain("complete");

        // Verify via list
        var listResult = _plugin.ListTodos("completed");
        var items = JsonSerializer.Deserialize<JsonElement[]>(listResult)!;
        items.Should().HaveCount(1);
    }

    [Fact]
    public void CompleteTodo_NotFound_ReturnsMessage()
    {
        var result = _plugin.CompleteTodo("nonexistent");
        result.Should().StartWith("Todo not found:");
    }

    // ── remove_todo ───────────────────────────────────────────────────────────

    [Fact]
    public void RemoveTodo_DeletesItem()
    {
        var addResult = JsonSerializer.Deserialize<JsonElement>(_plugin.AddTodo("To remove"));
        var id = addResult.GetProperty("Id").GetString()!;

        var result = _plugin.RemoveTodo(id);
        result.Should().Contain("Removed");

        var listResult = _plugin.ListTodos();
        listResult.Should().Be("[]");
    }

    [Fact]
    public void RemoveTodo_NotFound_ReturnsMessage()
    {
        var result = _plugin.RemoveTodo("nonexistent");
        result.Should().StartWith("Todo not found:");
    }
}
