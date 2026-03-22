using AgentApp.Backend.Services;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NSubstitute;

namespace AgentApp.Backend.Tests.Services;

[Trait("Category", "Integration")]
public class ScriptSandboxTests : IDisposable
{
    private readonly string _tempDir;
    private readonly ScriptSandbox _sandbox;

    public ScriptSandboxTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "sandbox_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workspace:Root"] = _tempDir
            })
            .Build();

        _sandbox = new ScriptSandbox(config, Substitute.For<ILogger<ScriptSandbox>>());
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    [Fact]
    public async Task RunAsync_CapturesStdout()
    {
        var result = await _sandbox.RunAsync("console.log('hello')");

        result.Should().Contain("exit code: 0");
        result.Should().Contain("stdout:");
        result.Should().Contain("hello");
    }

    [Fact]
    public async Task RunAsync_CapturesStderr()
    {
        var result = await _sandbox.RunAsync("console.error('oops')");

        result.Should().Contain("stderr:");
        result.Should().Contain("oops");
    }

    [Fact]
    public async Task RunAsync_NonzeroExitCode()
    {
        var result = await _sandbox.RunAsync("Deno.exit(42)");

        result.Should().Contain("exit code: 42");
    }

    [Fact]
    public async Task RunAsync_EmptyScript()
    {
        var result = await _sandbox.RunAsync("// nothing");

        result.Should().Contain("exit code: 0");
        result.Should().NotContain("stdout:");
    }

    [Fact]
    public async Task RunAsync_CombinedOutput()
    {
        var result = await _sandbox.RunAsync("""
            console.log("out");
            console.error("err");
            """);

        result.Should().Contain("stdout:");
        result.Should().Contain("out");
        result.Should().Contain("stderr:");
        result.Should().Contain("err");
    }

    [Fact]
    public async Task RunFileAsync_ExecutesExistingFile()
    {
        var scriptPath = Path.Combine(_tempDir, "test.ts");
        File.WriteAllText(scriptPath, "console.log('from file')");

        var result = await _sandbox.RunFileAsync(scriptPath);

        result.Should().Contain("exit code: 0");
        result.Should().Contain("from file");
    }

    [Fact]
    public async Task RunFileAsync_NonexistentFile_ReturnsError()
    {
        var result = await _sandbox.RunFileAsync(
            Path.Combine(_tempDir, "nope.ts"));

        result.Should().Contain("exit code: 1");
        result.Should().Contain("stderr:");
    }
}
