using System.Text.Json;
using AgentApp.Backend.Plugins;
using AgentApp.Backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;

namespace AgentApp.Backend.Tests.Plugins;

public class FileSystemPluginTests : IDisposable
{
    private readonly string _tempDir;
    private readonly IScriptSandbox _sandbox;
    private readonly FileSystemPlugin _plugin;

    public FileSystemPluginTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "fsp_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        _sandbox = Substitute.For<IScriptSandbox>();

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workspace:Root"] = _tempDir
            })
            .Build();

        _plugin = new FileSystemPlugin(_sandbox, config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    // ── ls ────────────────────────────────────────────────────────────────────

    [Fact]
    public void Ls_EmptyDirectory_ReturnsEmptyArray()
    {
        var result = _plugin.ListDirectory(".");
        result.Should().Be("[]");
    }

    [Fact]
    public void Ls_WithFilesAndDirs_ReturnsCorrectEntries()
    {
        File.WriteAllText(Path.Combine(_tempDir, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(_tempDir, "b.txt"), "world!");
        Directory.CreateDirectory(Path.Combine(_tempDir, "sub"));

        var result = _plugin.ListDirectory(".");
        var entries = JsonSerializer.Deserialize<JsonElement[]>(result)!;

        entries.Should().HaveCount(3);

        // Directories should sort before files
        entries[0].GetProperty("name").GetString().Should().Be("sub");
        entries[0].GetProperty("isDirectory").GetBoolean().Should().BeTrue();

        entries[1].GetProperty("name").GetString().Should().Be("a.txt");
        entries[1].GetProperty("size").GetInt64().Should().Be(5);

        entries[2].GetProperty("name").GetString().Should().Be("b.txt");
    }

    [Fact]
    public void Ls_NonexistentDirectory_ReturnsNotFound()
    {
        var result = _plugin.ListDirectory("nope");
        result.Should().StartWith("Directory not found:");
    }

    [Fact]
    public void Ls_PathTraversal_Throws()
    {
        var act = () => _plugin.ListDirectory("../../etc");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Access denied*");
    }

    // ── read_file ─────────────────────────────────────────────────────────────

    [Fact]
    public void ReadFile_ReadsEntireFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "hello world");

        var result = _plugin.ReadFile("test.txt");
        result.Should().Be("hello world");
    }

    [Fact]
    public void ReadFile_WithStartOffset()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "abcdefgh");

        var result = _plugin.ReadFile("test.txt", start: 3);
        result.Should().Be("defgh");
    }

    [Fact]
    public void ReadFile_WithMaxChars()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "abcdefgh");

        var result = _plugin.ReadFile("test.txt", maxChars: 3);
        result.Should().Be("abc");
    }

    [Fact]
    public void ReadFile_WithStartAndMaxChars()
    {
        File.WriteAllText(Path.Combine(_tempDir, "test.txt"), "abcdefgh");

        var result = _plugin.ReadFile("test.txt", start: 2, maxChars: 3);
        result.Should().Be("cde");
    }

    [Fact]
    public void ReadFile_FileNotFound_ReturnsMessage()
    {
        var result = _plugin.ReadFile("missing.txt");
        result.Should().StartWith("File not found:");
    }

    [Fact]
    public void ReadFile_PathTraversal_Throws()
    {
        var act = () => _plugin.ReadFile("../../../etc/passwd");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Access denied*");
    }

    // ── write_file ────────────────────────────────────────────────────────────

    [Fact]
    public void WriteFile_CreatesFileAndParentDirs()
    {
        var result = _plugin.WriteFile("deep/nested/file.txt", "content");

        result.Should().Contain("Written 7 characters");
        File.ReadAllText(Path.Combine(_tempDir, "deep", "nested", "file.txt"))
            .Should().Be("content");
    }

    [Fact]
    public void WriteFile_OverwritesExistingFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "existing.txt"), "old");

        _plugin.WriteFile("existing.txt", "new content");

        File.ReadAllText(Path.Combine(_tempDir, "existing.txt"))
            .Should().Be("new content");
    }

    [Fact]
    public void WriteFile_PathTraversal_Throws()
    {
        var act = () => _plugin.WriteFile("../../evil.txt", "data");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Access denied*");
    }

    // ── response_include ──────────────────────────────────────────────────────

    [Fact]
    public void ResponseInclude_ReadsMarkdownFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "doc.md"), "# Title\nHello");

        var result = _plugin.ResponseInclude("doc.md");
        result.Should().Be("# Title\nHello");
    }

    [Fact]
    public void ResponseInclude_FileNotFound()
    {
        var result = _plugin.ResponseInclude("missing.md");
        result.Should().StartWith("File not found:");
    }

    // ── exec_script ───────────────────────────────────────────────────────────

    [Fact]
    public async Task ExecScript_DelegatesToSandbox()
    {
        _sandbox.RunAsync("console.log('hi')")
            .Returns("exit code: 0\nstdout:\nhi");

        var result = await _plugin.ExecScriptAsync("console.log('hi')");

        result.Should().Be("exit code: 0\nstdout:\nhi");
        await _sandbox.Received(1).RunAsync("console.log('hi')");
    }

    // ── exec_script_file ──────────────────────────────────────────────────────

    [Fact]
    public async Task ExecScriptFile_DelegatesToSandbox()
    {
        var scriptPath = Path.Combine(_tempDir, "test.ts");
        File.WriteAllText(scriptPath, "console.log('from file')");

        _sandbox.RunFileAsync(scriptPath)
            .Returns("exit code: 0\nstdout:\nfrom file");

        var result = await _plugin.ExecScriptFileAsync("test.ts");

        result.Should().Be("exit code: 0\nstdout:\nfrom file");
        await _sandbox.Received(1).RunFileAsync(scriptPath);
    }

    [Fact]
    public async Task ExecScriptFile_FileNotFound()
    {
        var result = await _plugin.ExecScriptFileAsync("nope.ts");
        result.Should().StartWith("File not found:");
    }
}
