using System.Diagnostics;
using System.Text;

namespace AgentApp.Backend.Services;

public interface IScriptSandbox
{
    Task<string> RunAsync(string scriptContent, CancellationToken ct = default);
    Task<string> RunFileAsync(string scriptPath, CancellationToken ct = default);
}

public class ScriptSandbox(IConfiguration config, ILogger<ScriptSandbox> logger) : IScriptSandbox
{
    private readonly string _root = Path.GetFullPath(
        config["Workspace:Root"] ?? ".");

    public async Task<string> RunAsync(string scriptContent, CancellationToken ct = default)
    {
        var tmpFile = Path.GetTempFileName() + ".ts";
        await File.WriteAllTextAsync(tmpFile, scriptContent, ct);

        try
        {
            return await RunFileAsync(tmpFile, ct);
        }
        finally
        {
            try { File.Delete(tmpFile); } catch { /* best effort */ }
        }
    }

    public async Task<string> RunFileAsync(string scriptPath, CancellationToken ct = default)
    {
        var psi = new ProcessStartInfo
        {
            FileName = "deno",
            Arguments = $"run --allow-read=\"{_root}\" --allow-write=\"{_root}\" --allow-import \"{scriptPath}\"",
            RedirectStandardOutput = true,
            RedirectStandardError = true,
            UseShellExecute = false,
            WorkingDirectory = _root
        };

        using var process = Process.Start(psi)
            ?? throw new InvalidOperationException("Failed to start deno process.");

        var stdoutTask = process.StandardOutput.ReadToEndAsync(ct);
        var stderrTask = process.StandardError.ReadToEndAsync(ct);

        await process.WaitForExitAsync(ct);

        var stdout = await stdoutTask;
        var stderr = await stderrTask;

        logger.LogDebug("Deno exit {Code}, stdout={Out}", process.ExitCode, stdout);

        var result = new StringBuilder();
        result.AppendLine($"exit code: {process.ExitCode}");

        if (!string.IsNullOrWhiteSpace(stdout))
        {
            result.AppendLine("stdout:");
            result.AppendLine(stdout.TrimEnd());
        }

        if (!string.IsNullOrWhiteSpace(stderr))
        {
            result.AppendLine("stderr:");
            result.AppendLine(stderr.TrimEnd());
        }

        return result.ToString().TrimEnd();
    }
}
