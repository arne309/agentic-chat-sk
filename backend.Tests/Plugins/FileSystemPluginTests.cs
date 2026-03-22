using System.Text.Json;
using AgentApp.Backend.Models;
using AgentApp.Backend.Plugins;
using AgentApp.Backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using NSubstitute;
using Parquet;
using Parquet.Schema;

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

    // ── mkdir ─────────────────────────────────────────────────────────────────

    [Fact]
    public void MakeDirectory_CreatesDirectory()
    {
        var result = _plugin.MakeDirectory("newdir");
        result.Should().Contain("Created directory:");
        Directory.Exists(Path.Combine(_tempDir, "newdir")).Should().BeTrue();
    }

    [Fact]
    public void MakeDirectory_NestedPath_CreatesParentDirs()
    {
        var result = _plugin.MakeDirectory("a/b/c");
        result.Should().Contain("Created directory:");
        Directory.Exists(Path.Combine(_tempDir, "a", "b", "c")).Should().BeTrue();
    }

    [Fact]
    public void MakeDirectory_AlreadyExists_Succeeds()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "existing"));
        var result = _plugin.MakeDirectory("existing");
        result.Should().Contain("Created directory:");
    }

    [Fact]
    public void MakeDirectory_PathTraversal_Throws()
    {
        var act = () => _plugin.MakeDirectory("../../evil");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Access denied*");
    }

    // ── rmdir ─────────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveDirectory_RemovesEmptyDirectory()
    {
        Directory.CreateDirectory(Path.Combine(_tempDir, "todelete"));

        var result = _plugin.RemoveDirectory("todelete");
        result.Should().Contain("Removed directory:");
        Directory.Exists(Path.Combine(_tempDir, "todelete")).Should().BeFalse();
    }

    [Fact]
    public void RemoveDirectory_NotEmpty_Fails()
    {
        var dir = Path.Combine(_tempDir, "notempty");
        Directory.CreateDirectory(dir);
        File.WriteAllText(Path.Combine(dir, "file.txt"), "content");

        var result = _plugin.RemoveDirectory("notempty");
        result.Should().Contain("Directory is not empty:");
        Directory.Exists(dir).Should().BeTrue();
    }

    [Fact]
    public void RemoveDirectory_NotFound_ReturnsMessage()
    {
        var result = _plugin.RemoveDirectory("nosuchdir");
        result.Should().StartWith("Directory not found:");
    }

    [Fact]
    public void RemoveDirectory_PathTraversal_Throws()
    {
        var act = () => _plugin.RemoveDirectory("../../evil");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Access denied*");
    }

    // ── rm ────────────────────────────────────────────────────────────────────

    [Fact]
    public void RemoveFile_DeletesFile()
    {
        File.WriteAllText(Path.Combine(_tempDir, "deleteme.txt"), "bye");

        var result = _plugin.RemoveFile("deleteme.txt");
        result.Should().Contain("Removed file:");
        File.Exists(Path.Combine(_tempDir, "deleteme.txt")).Should().BeFalse();
    }

    [Fact]
    public void RemoveFile_NotFound_ReturnsMessage()
    {
        var result = _plugin.RemoveFile("nosuchfile.txt");
        result.Should().StartWith("File not found:");
    }

    [Fact]
    public void RemoveFile_PathTraversal_Throws()
    {
        var act = () => _plugin.RemoveFile("../../evil.txt");
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Access denied*");
    }

    // ── response_show_parquet ─────────────────────────────────────────────────

    [Fact]
    public async Task ResponseShowParquet_ReadsParquetFile()
    {
        var parquetPath = Path.Combine(_tempDir, "test.parquet");
        await CreateTestParquetFile(parquetPath, 5);

        var result = await _plugin.ResponseShowParquetAsync("test.parquet");
        var preview = JsonSerializer.Deserialize<JsonElement>(result);

        preview.GetProperty("totalRowCount").GetInt64().Should().Be(5);
        preview.GetProperty("previewRowCount").GetInt32().Should().Be(5);

        var columns = preview.GetProperty("columns").EnumerateArray().ToList();
        columns.Should().HaveCount(3);
        columns[0].GetProperty("name").GetString().Should().Be("id");
        columns[1].GetProperty("name").GetString().Should().Be("name");
        columns[2].GetProperty("name").GetString().Should().Be("score");

        var rows = preview.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().HaveCount(5);
    }

    [Fact]
    public async Task ResponseShowParquet_LargeFile_LimitsTo100Rows()
    {
        var parquetPath = Path.Combine(_tempDir, "large.parquet");
        await CreateTestParquetFile(parquetPath, 200);

        var result = await _plugin.ResponseShowParquetAsync("large.parquet");
        var preview = JsonSerializer.Deserialize<JsonElement>(result);

        preview.GetProperty("totalRowCount").GetInt64().Should().Be(200);
        preview.GetProperty("previewRowCount").GetInt32().Should().Be(100);

        var rows = preview.GetProperty("rows").EnumerateArray().ToList();
        rows.Should().HaveCount(100);
    }

    [Fact]
    public async Task ResponseShowParquet_FileNotFound()
    {
        var result = await _plugin.ResponseShowParquetAsync("missing.parquet");
        result.Should().StartWith("File not found:");
    }

    [Fact]
    public void ResponseShowParquet_PathTraversal_Throws()
    {
        var act = () => _plugin.ResponseShowParquetAsync("../../evil.parquet");
        act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Access denied*");
    }

    private static async Task CreateTestParquetFile(string path, int rowCount)
    {
        var schema = new ParquetSchema(
            new DataField<int>("id"),
            new DataField<string>("name"),
            new DataField<double>("score"));

        var ids = Enumerable.Range(1, rowCount).ToArray();
        var names = Enumerable.Range(1, rowCount).Select(i => $"item_{i}").ToArray();
        var scores = Enumerable.Range(1, rowCount).Select(i => i * 1.5).ToArray();

        using var stream = File.Create(path);
        using var writer = await ParquetWriter.CreateAsync(schema, stream);
        using var rg = writer.CreateRowGroup();
        await rg.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[0], ids));
        await rg.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[1], names));
        await rg.WriteColumnAsync(new Parquet.Data.DataColumn(schema.DataFields[2], scores));
    }
}
