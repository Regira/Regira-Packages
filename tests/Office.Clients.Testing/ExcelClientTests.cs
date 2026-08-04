using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Office.Excel.Abstractions;
using Regira.Office.Excel.Models;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class ExcelClientTests : OfficeClientTestsBase
{
    private IExcelService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = Services.GetRequiredService<IExcelService>();
    }

    [Test]
    public async Task Create_Excel()
    {
        var sheet = new ExcelSheet
        {
            Name = "People",
            Data = new List<object>
            {
                new { Name = "Alice", Age = 30, Active = true },
                new { Name = "Bob", Age = 25, Active = false },
                new { Name = "Charlie", Age = 35, Active = true }
            }
        };

        using var result = await _service.Create([sheet]);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task Create_MultipleSheets()
    {
        var sheet1 = new ExcelSheet
        {
            Name = "Sheet1",
            Data = new List<object> { new { Id = 1, Value = "A" }, new { Id = 2, Value = "B" } }
        };
        var sheet2 = new ExcelSheet
        {
            Name = "Sheet2",
            Data = new List<object> { new { Id = 3, Value = "C" }, new { Id = 4, Value = "D" } }
        };

        using var result = await _service.Create([sheet1, sheet2]);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task Create_And_Read_RoundTrip()
    {
        var data = new List<object>
        {
            new { Name = "Alice", Score = 95 },
            new { Name = "Bob", Score = 87 }
        };
        var sheet = new ExcelSheet { Name = "Scores", Data = data };

        using var excelFile = await _service.Create([sheet]);
        var readFile = new BinaryFileItem { Bytes = excelFile.GetBytes()! };
        var sheets = (await _service.Read(readFile)).ToList();

        Assert.That(sheets, Is.Not.Empty);
        Assert.That(sheets[0].Data, Is.Not.Null);
        Assert.That(sheets[0].Data!.Count, Is.EqualTo(data.Count));
    }

    [Test]
    public async Task Read_Excel()
    {
        var testsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        var inputPath = Path.Combine(testsDir, "Office.Excel.Testing", "Assets", "Input", "input-with-duplicates.xlsx");
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(inputPath) };

        var sheets = (await _service.Read(file)).ToList();

        Assert.That(sheets, Is.Not.Empty);
        Assert.That(sheets[0].Data, Is.Not.Null);
        Assert.That(sheets[0].Data, Is.Not.Empty);
    }
}
