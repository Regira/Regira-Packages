using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Office.Csv.Abstractions;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class CsvClientTests : OfficeClientTestsBase
{
    private const string SampleCsv = "Id,Name,Value\r\n1,Alpha,10.5\r\n2,Beta,20.0\r\n3,Gamma,30.75";

    private readonly string _inputDir;

    private ICsvService _service = null!;

    public CsvClientTests()
    {
        var testsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        _inputDir = Path.Combine(testsDir, "Office.Csv.Testing", "Assets", "Input");
    }

    [SetUp]
    public void Setup()
    {
        _service = Services.GetRequiredService<ICsvService>();
    }

    [Test]
    public async Task Parse_CsvString()
    {
        var rows = await _service.Read(SampleCsv);

        Assert.That(rows, Is.Not.Null);
        Assert.That(rows, Has.Count.EqualTo(3));
        Assert.That(rows[0]["Name"].ToString(), Is.EqualTo("Alpha"));
    }

    [Test]
    public async Task Parse_CsvFile()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "cities.csv")) };

        var rows = await _service.Read(file);

        Assert.That(rows, Is.Not.Null);
        Assert.That(rows, Is.Not.Empty);
    }

    [Test]
    public async Task Write_To_String()
    {
        var data = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Id"] = 1, ["Name"] = "Alpha", ["Value"] = 10.5 },
            new Dictionary<string, object> { ["Id"] = 2, ["Name"] = "Beta", ["Value"] = 20.0 }
        };

        var csv = await _service.Write(data);

        Assert.That(csv, Is.Not.Null);
        Assert.That(csv, Is.Not.Empty);
        Assert.That(csv, Does.Contain("Alpha"));
    }

    [Test]
    public async Task Write_To_File()
    {
        var data = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Id"] = 1, ["Name"] = "Alpha" },
            new Dictionary<string, object> { ["Id"] = 2, ["Name"] = "Beta" }
        };

        using var result = await _service.WriteFile(data);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task Write_And_Read_RoundTrip()
    {
        var original = new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Id"] = "1", ["Name"] = "Alpha" },
            new Dictionary<string, object> { ["Id"] = "2", ["Name"] = "Beta" },
            new Dictionary<string, object> { ["Id"] = "3", ["Name"] = "Gamma" }
        };

        var csv = await _service.Write(original);
        var parsed = await _service.Read(csv);

        Assert.That(parsed, Has.Count.EqualTo(original.Count));
        for (var i = 0; i < original.Count; i++)
        {
            Assert.That(parsed[i]["Name"].ToString(), Is.EqualTo(original[i]["Name"].ToString()));
        }
    }
}
