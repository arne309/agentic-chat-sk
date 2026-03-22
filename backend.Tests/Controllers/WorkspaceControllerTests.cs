using System.Text;
using AgentApp.Backend.Controllers;
using ClosedXML.Excel;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Parquet;
using Parquet.Data;
using Parquet.Schema;

namespace AgentApp.Backend.Tests.Controllers;

public class WorkspaceControllerTests : IDisposable
{
    private readonly string _tempDir;
    private readonly WorkspaceController _controller;

    public WorkspaceControllerTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), "ws_ctrl_test_" + Guid.NewGuid());
        Directory.CreateDirectory(_tempDir);

        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Workspace:Root"] = _tempDir
            })
            .Build();

        _controller = new WorkspaceController(config);
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, recursive: true);
    }

    private async Task CreateTestParquetFile(string name, int rowCount)
    {
        var schema = new ParquetSchema(
            new DataField<int>("id"),
            new DataField<string>("name"),
            new DataField<double>("score"));

        var ids = Enumerable.Range(1, rowCount).ToArray();
        var names = Enumerable.Range(1, rowCount).Select(i => $"item_{i}").ToArray();
        var scores = Enumerable.Range(1, rowCount).Select(i => i * 1.5).ToArray();

        using var stream = File.Create(Path.Combine(_tempDir, name));
        using var writer = await ParquetWriter.CreateAsync(schema, stream);
        using var rg = writer.CreateRowGroup();
        await rg.WriteColumnAsync(new DataColumn(schema.DataFields[0], ids));
        await rg.WriteColumnAsync(new DataColumn(schema.DataFields[1], names));
        await rg.WriteColumnAsync(new DataColumn(schema.DataFields[2], scores));
    }

    [Fact]
    public async Task Download_Parquet_ReturnsOriginalFile()
    {
        await CreateTestParquetFile("data.parquet", 5);

        var result = await _controller.Download("data.parquet", "parquet");

        result.Should().BeOfType<FileStreamResult>();
        var fileResult = (FileStreamResult)result;
        fileResult.ContentType.Should().Be("application/octet-stream");
        fileResult.FileDownloadName.Should().Be("data.parquet");
        fileResult.FileStream.Length.Should().BeGreaterThan(0);
        fileResult.FileStream.Dispose();
    }

    [Fact]
    public async Task Download_Csv_ReturnsValidCsv()
    {
        await CreateTestParquetFile("data.parquet", 3);

        var result = await _controller.Download("data.parquet", "csv");

        result.Should().BeOfType<FileContentResult>();
        var fileResult = (FileContentResult)result;
        fileResult.ContentType.Should().Be("text/csv");
        fileResult.FileDownloadName.Should().Be("data.csv");

        var csv = Encoding.UTF8.GetString(fileResult.FileContents);
        var lines = csv.Split('\n', StringSplitOptions.RemoveEmptyEntries);
        lines[0].Trim().Should().Be("id,name,score");
        lines.Should().HaveCount(4); // header + 3 data rows
    }

    [Fact]
    public async Task Download_Xlsx_ReturnsValidXlsx()
    {
        await CreateTestParquetFile("data.parquet", 3);

        var result = await _controller.Download("data.parquet", "xlsx");

        result.Should().BeOfType<FileStreamResult>();
        var fileResult = (FileStreamResult)result;
        fileResult.ContentType.Should().Be("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        fileResult.FileDownloadName.Should().Be("data.xlsx");

        // Verify ClosedXML can read it back
        using var workbook = new XLWorkbook(fileResult.FileStream);
        var ws = workbook.Worksheets.First();
        ws.Cell(1, 1).GetString().Should().Be("id");
        ws.Cell(1, 2).GetString().Should().Be("name");
        ws.Cell(1, 3).GetString().Should().Be("score");
        ws.Cell(2, 1).GetValue<int>().Should().Be(1);
        ws.Cell(2, 2).GetString().Should().Be("item_1");
        ws.LastRowUsed()!.RowNumber().Should().Be(4); // header + 3 rows
    }

    [Fact]
    public async Task Download_UnsupportedFormat_ReturnsBadRequest()
    {
        await CreateTestParquetFile("data.parquet", 1);

        var result = await _controller.Download("data.parquet", "json");

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Download_FileNotFound_ReturnsNotFound()
    {
        var result = await _controller.Download("missing.parquet", "csv");
        result.Should().BeOfType<NotFoundResult>();
    }

    [Fact]
    public async Task Download_PathTraversal_ReturnsBadRequest()
    {
        var result = await _controller.Download("../../etc/passwd", "parquet");
        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
