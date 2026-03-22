using System.ComponentModel;
using System.Text.Json;
using AgentApp.Backend.Services;
using Microsoft.SemanticKernel;

namespace AgentApp.Backend.Plugins;

public class FileSystemPlugin(ScriptSandbox sandbox, IConfiguration config)
{
    private readonly string _root = Path.GetFullPath(
        config["Workspace:Root"] ?? ".");

    [KernelFunction("ls")]
    [Description("List files and subdirectories in a directory. Returns a JSON array of {name, size, isDirectory}.")]
    public string ListDirectory(
        [Description("Directory path relative to the workspace root. Defaults to root.")] string path = ".")
    {
        var fullPath = ResolveSafe(path);
        if (!Directory.Exists(fullPath))
            return $"Directory not found: {path}";

        var entries = Directory.EnumerateFileSystemEntries(fullPath)
            .Select(e =>
            {
                var isDir = Directory.Exists(e);
                var size = isDir ? 0L : new FileInfo(e).Length;
                return new { name = Path.GetFileName(e), size, isDirectory = isDir };
            })
            .OrderBy(e => !e.isDirectory)
            .ThenBy(e => e.name)
            .ToList();

        return JsonSerializer.Serialize(entries);
    }

    [KernelFunction("read_file")]
    [Description("Read the content of a file. Optionally specify a start character offset and max characters to read.")]
    public string ReadFile(
        [Description("File path relative to workspace root")] string path,
        [Description("Start character offset (optional, 0-based)")] int? start = null,
        [Description("Maximum number of characters to return (optional)")] int? maxChars = null)
    {
        var fullPath = ResolveSafe(path);
        if (!File.Exists(fullPath))
            return $"File not found: {path}";

        var content = File.ReadAllText(fullPath);

        if (start.HasValue || maxChars.HasValue)
        {
            var s = Math.Max(0, start ?? 0);
            var len = maxChars.HasValue ? Math.Min(maxChars.Value, content.Length - s) : content.Length - s;
            content = content.Substring(s, Math.Max(0, len));
        }

        return content;
    }

    [KernelFunction("write_file")]
    [Description("Write content to a file, creating it (and any parent directories) if needed.")]
    public string WriteFile(
        [Description("File path relative to workspace root")] string path,
        [Description("Content to write to the file")] string content)
    {
        var fullPath = ResolveSafe(path);
        Directory.CreateDirectory(Path.GetDirectoryName(fullPath)!);
        File.WriteAllText(fullPath, content);
        return $"Written {content.Length} characters to {path}";
    }

    [KernelFunction("exec_script")]
    [Description("""
        Execute an inline TypeScript script using the Deno runtime.
        The script runs with read/write access limited to the workspace directory and no general network access.
        Use Deno module specifiers to import packages: npm:package-name, jsr:@scope/package, or https://esm.sh/package.
        Example imports: import { z } from "npm:zod"; import { join } from "jsr:@std/path";
        """)]
    public async Task<string> ExecScriptAsync(
        [Description("TypeScript source code to execute")] string scriptContent)
    {
        return await sandbox.RunAsync(scriptContent);
    }

    [KernelFunction("exec_script_file")]
    [Description("""
        Execute a TypeScript script file that already exists in the workspace using the Deno runtime.
        The script runs with read/write access limited to the workspace directory and no general network access.
        Use Deno module specifiers to import packages: npm:package-name, jsr:@scope/package, or https://esm.sh/package.
        Example imports: import { z } from "npm:zod"; import { join } from "jsr:@std/path";
        """)]
    public async Task<string> ExecScriptFileAsync(
        [Description("Path to the .ts file relative to workspace root")] string path)
    {
        var fullPath = ResolveSafe(path);
        if (!File.Exists(fullPath))
            return $"File not found: {path}";
        return await sandbox.RunFileAsync(fullPath);
    }

    [KernelFunction("response_include")]
    [Description("Use when showing large amounts of data directly to the user. Reads a Markdown file from the workspace and renders it inline in the conversation as a document card.")]
    public string ResponseInclude(
        [Description("Path to the .md file relative to workspace root")] string path)
    {
        var fullPath = ResolveSafe(path);
        if (!File.Exists(fullPath))
            return $"File not found: {path}";
        return File.ReadAllText(fullPath);
    }

    private string ResolveSafe(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Access denied: path '{path}' is outside the workspace.");
        return full;
    }
}
