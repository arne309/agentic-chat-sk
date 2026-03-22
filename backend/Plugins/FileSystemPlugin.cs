using System.ComponentModel;
using System.Text.Json;
using AgentApp.Backend.Models;
using AgentApp.Backend.Services;
using Microsoft.SemanticKernel;
using Parquet;
using Parquet.Schema;

namespace AgentApp.Backend.Plugins;

public class FileSystemPlugin(IScriptSandbox sandbox, IConfiguration config)
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

    [KernelFunction("mkdir")]
    [Description("Create a directory (and any parent directories) in the workspace. Succeeds silently if the directory already exists.")]
    public string MakeDirectory(
        [Description("Directory path relative to workspace root")] string path)
    {
        var fullPath = ResolveSafe(path);
        Directory.CreateDirectory(fullPath);
        return $"Created directory: {path}";
    }

    [KernelFunction("rmdir")]
    [Description("Remove an empty directory from the workspace. Fails if the directory is not empty.")]
    public string RemoveDirectory(
        [Description("Directory path relative to workspace root")] string path)
    {
        var fullPath = ResolveSafe(path);
        if (!Directory.Exists(fullPath))
            return $"Directory not found: {path}";
        if (Directory.EnumerateFileSystemEntries(fullPath).Any())
            return $"Directory is not empty: {path}";
        Directory.Delete(fullPath, recursive: false);
        return $"Removed directory: {path}";
    }

    [KernelFunction("rm")]
    [Description("Remove a file from the workspace.")]
    public string RemoveFile(
        [Description("File path relative to workspace root")] string path)
    {
        var fullPath = ResolveSafe(path);
        if (!File.Exists(fullPath))
            return $"File not found: {path}";
        File.Delete(fullPath);
        return $"Removed file: {path}";
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

    [KernelFunction("response_show_parquet")]
    [Description("Load a Parquet file and display its contents as an interactive data table in the conversation. The user can download the data as CSV, Parquet, or XLSX.")]
    public async Task<string> ResponseShowParquetAsync(
        [Description("Path to the .parquet file relative to workspace root")] string path)
    {
        var fullPath = ResolveSafe(path);
        if (!File.Exists(fullPath))
            return $"File not found: {path}";

        using var stream = File.OpenRead(fullPath);
        using var reader = await ParquetReader.CreateAsync(stream);

        var dataFields = reader.Schema.GetDataFields();
        var columns = dataFields.Select(f => new DataColumnInfo
        {
            Name = f.Name,
            Type = MapParquetType(f.ClrType)
        }).ToList();

        // Calculate total row count from row group metadata
        long totalRowCount = 0;
        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            using var rg = reader.OpenRowGroupReader(i);
            totalRowCount += rg.RowCount;
        }

        // Read up to 100 preview rows
        const int maxPreviewRows = 100;
        var rows = new List<List<object?>>();
        int remaining = maxPreviewRows;

        for (int i = 0; i < reader.RowGroupCount && remaining > 0; i++)
        {
            using var rg = reader.OpenRowGroupReader(i);
            var columnArrays = new Array[dataFields.Length];
            for (int c = 0; c < dataFields.Length; c++)
            {
                var col = await rg.ReadColumnAsync(dataFields[c]);
                columnArrays[c] = col.Data;
            }

            var rowsInGroup = (int)Math.Min(rg.RowCount, remaining);
            for (int r = 0; r < rowsInGroup; r++)
            {
                var row = new List<object?>();
                for (int c = 0; c < dataFields.Length; c++)
                    row.Add(columnArrays[c].GetValue(r));
                rows.Add(row);
            }
            remaining -= rowsInGroup;
        }

        var preview = new
        {
            columns,
            rows,
            totalRowCount,
            previewRowCount = rows.Count
        };

        return JsonSerializer.Serialize(preview);
    }

    private static string MapParquetType(Type clrType)
    {
        var underlying = Nullable.GetUnderlyingType(clrType) ?? clrType;
        if (underlying == typeof(string)) return "string";
        if (underlying == typeof(int) || underlying == typeof(long) ||
            underlying == typeof(short) || underlying == typeof(byte)) return "int";
        if (underlying == typeof(float) || underlying == typeof(double) ||
            underlying == typeof(decimal)) return "double";
        if (underlying == typeof(bool)) return "bool";
        if (underlying == typeof(DateTime) || underlying == typeof(DateTimeOffset)) return "datetime";
        return "string";
    }

    private string ResolveSafe(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Access denied: path '{path}' is outside the workspace.");
        return full;
    }
}
