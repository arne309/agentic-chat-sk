using System.Globalization;
using System.Text;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc;
using Parquet;
using Parquet.Schema;

namespace AgentApp.Backend.Controllers;

[ApiController]
[Route("api/workspace")]
public class WorkspaceController(IConfiguration config) : ControllerBase
{
    private readonly string _root = Path.GetFullPath(
        config["Workspace:Root"] ?? ".");

    [HttpGet("download")]
    public async Task<IActionResult> Download(
        [FromQuery] string path,
        [FromQuery] string format = "parquet")
    {
        string fullPath;
        try
        {
            fullPath = ResolveSafe(path);
        }
        catch (InvalidOperationException)
        {
            return BadRequest("Access denied");
        }

        if (!System.IO.File.Exists(fullPath))
            return NotFound();

        var baseName = Path.GetFileNameWithoutExtension(path);

        return format.ToLowerInvariant() switch
        {
            "parquet" => ServeParquet(fullPath, baseName),
            "csv" => await ServeCsvAsync(fullPath, baseName),
            "xlsx" => await ServeXlsxAsync(fullPath, baseName),
            _ => BadRequest($"Unsupported format: {format}")
        };
    }

    private IActionResult ServeParquet(string fullPath, string baseName)
    {
        var stream = System.IO.File.OpenRead(fullPath);
        return File(stream, "application/octet-stream", $"{baseName}.parquet");
    }

    private async Task<IActionResult> ServeCsvAsync(string fullPath, string baseName)
    {
        using var stream = System.IO.File.OpenRead(fullPath);
        using var reader = await ParquetReader.CreateAsync(stream);

        var dataFields = reader.Schema.GetDataFields();
        var sb = new StringBuilder();

        // Header
        sb.AppendLine(string.Join(",", dataFields.Select(f => CsvQuote(f.Name))));

        // Rows
        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            using var rg = reader.OpenRowGroupReader(i);
            var columnArrays = new Array[dataFields.Length];
            for (int c = 0; c < dataFields.Length; c++)
            {
                var col = await rg.ReadColumnAsync(dataFields[c]);
                columnArrays[c] = col.Data;
            }

            for (int r = 0; r < rg.RowCount; r++)
            {
                var values = new string[dataFields.Length];
                for (int c = 0; c < dataFields.Length; c++)
                {
                    var val = columnArrays[c].GetValue(r);
                    values[c] = CsvQuote(val?.ToString() ?? "");
                }
                sb.AppendLine(string.Join(",", values));
            }
        }

        var bytes = Encoding.UTF8.GetBytes(sb.ToString());
        return File(bytes, "text/csv", $"{baseName}.csv");
    }

    private async Task<IActionResult> ServeXlsxAsync(string fullPath, string baseName)
    {
        using var stream = System.IO.File.OpenRead(fullPath);
        using var reader = await ParquetReader.CreateAsync(stream);

        var dataFields = reader.Schema.GetDataFields();
        using var workbook = new XLWorkbook();
        var ws = workbook.Worksheets.Add("Data");

        // Header row
        for (int c = 0; c < dataFields.Length; c++)
            ws.Cell(1, c + 1).Value = dataFields[c].Name;

        // Data rows
        int rowOffset = 2;
        for (int i = 0; i < reader.RowGroupCount; i++)
        {
            using var rg = reader.OpenRowGroupReader(i);
            var columnArrays = new Array[dataFields.Length];
            for (int c = 0; c < dataFields.Length; c++)
            {
                var col = await rg.ReadColumnAsync(dataFields[c]);
                columnArrays[c] = col.Data;
            }

            for (int r = 0; r < rg.RowCount; r++)
            {
                for (int c = 0; c < dataFields.Length; c++)
                {
                    var val = columnArrays[c].GetValue(r);
                    var cell = ws.Cell(rowOffset + r, c + 1);
                    SetCellValue(cell, val);
                }
            }
            rowOffset += (int)rg.RowCount;
        }

        var ms = new MemoryStream();
        workbook.SaveAs(ms);
        ms.Position = 0;
        return File(ms,
            "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
            $"{baseName}.xlsx");
    }

    private static void SetCellValue(IXLCell cell, object? val)
    {
        switch (val)
        {
            case null:
                break;
            case int i:
                cell.Value = i;
                break;
            case long l:
                cell.Value = l;
                break;
            case short s:
                cell.Value = s;
                break;
            case float f:
                cell.Value = f;
                break;
            case double d:
                cell.Value = d;
                break;
            case decimal dec:
                cell.Value = dec;
                break;
            case bool b:
                cell.Value = b;
                break;
            case DateTime dt:
                cell.Value = dt;
                break;
            case DateTimeOffset dto:
                cell.Value = dto.DateTime;
                break;
            default:
                cell.Value = val.ToString();
                break;
        }
    }

    private static string CsvQuote(string value)
    {
        if (value.Contains(',') || value.Contains('"') || value.Contains('\n') || value.Contains('\r'))
            return "\"" + value.Replace("\"", "\"\"") + "\"";
        return value;
    }

    private string ResolveSafe(string path)
    {
        var full = Path.GetFullPath(Path.Combine(_root, path));
        if (!full.StartsWith(_root, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Access denied: path '{path}' is outside the workspace.");
        return full;
    }
}
