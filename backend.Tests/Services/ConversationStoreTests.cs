using AgentApp.Backend.Models;
using AgentApp.Backend.Services;
using FluentAssertions;

namespace AgentApp.Backend.Tests.Services;

public class ConversationStoreTests
{
    private readonly ConversationStore _store = new();

    [Fact]
    public void Create_ReturnsNewConversation()
    {
        var c = _store.Create();

        c.Id.Should().NotBeNullOrEmpty();
        c.Title.Should().Be("New conversation");
        c.Messages.Should().BeEmpty();
    }

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var c1 = _store.Create();
        var c2 = _store.Create();

        c1.Id.Should().NotBe(c2.Id);
    }

    [Fact]
    public void Get_ReturnsNullForUnknownId()
    {
        _store.Get("nonexistent").Should().BeNull();
    }

    [Fact]
    public void Get_ReturnsExistingConversation()
    {
        var c = _store.Create();
        _store.Get(c.Id).Should().BeSameAs(c);
    }

    [Fact]
    public void GetOrCreate_CreatesIfMissing()
    {
        var c = _store.GetOrCreate("abc");

        c.Id.Should().Be("abc");
        c.Title.Should().Be("New conversation");
    }

    [Fact]
    public void GetOrCreate_ReturnsExistingIfPresent()
    {
        var c1 = _store.GetOrCreate("abc");
        var c2 = _store.GetOrCreate("abc");

        c2.Should().BeSameAs(c1);
    }

    [Fact]
    public void Delete_RemovesExistingConversation()
    {
        var c = _store.Create();

        _store.Delete(c.Id).Should().BeTrue();
        _store.Get(c.Id).Should().BeNull();
    }

    [Fact]
    public void Delete_ReturnsFalseForUnknownId()
    {
        _store.Delete("nope").Should().BeFalse();
    }

    [Fact]
    public void GetAll_ReturnsAllConversations()
    {
        _store.Create();
        _store.Create();
        _store.Create();

        _store.GetAll().Should().HaveCount(3);
    }

    [Fact]
    public void GetAll_ReturnsSummariesWithCorrectFields()
    {
        var c = _store.Create();
        c.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Content = "hello" }]
        });

        var summaries = _store.GetAll();

        summaries.Should().ContainSingle()
            .Which.MessageCount.Should().Be(1);
    }

    [Fact]
    public void DeriveTitle_SetsFromFirstUserMessage()
    {
        var c = _store.Create();
        c.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Content = "How do I sort a list?" }]
        });

        _store.DeriveTitle(c);

        c.Title.Should().Be("How do I sort a list?");
    }

    [Fact]
    public void DeriveTitle_TruncatesLongTitles()
    {
        var c = _store.Create();
        var longText = new string('x', 80);
        c.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Content = longText }]
        });

        _store.DeriveTitle(c);

        c.Title.Should().HaveLength(60);
        c.Title.Should().EndWith("...");
    }

    [Fact]
    public void DeriveTitle_DoesNotOverwriteExistingTitle()
    {
        var c = _store.Create();
        c.Title = "Custom Title";
        c.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Parts = [new TextPart { Content = "ignored" }]
        });

        _store.DeriveTitle(c);

        c.Title.Should().Be("Custom Title");
    }

    [Fact]
    public void DeriveTitle_IgnoresAssistantMessages()
    {
        var c = _store.Create();
        c.Messages.Add(new Message
        {
            Role = MessageRole.Assistant,
            Parts = [new TextPart { Content = "I'm an assistant" }]
        });

        _store.DeriveTitle(c);

        c.Title.Should().Be("New conversation");
    }

    [Fact]
    public void DeriveTitle_ConcatenatesMultipleTextParts()
    {
        var c = _store.Create();
        c.Messages.Add(new Message
        {
            Role = MessageRole.User,
            Parts =
            [
                new TextPart { Content = "Hello " },
                new TextPart { Content = "World" }
            ]
        });

        _store.DeriveTitle(c);

        c.Title.Should().Be("Hello World");
    }
}
