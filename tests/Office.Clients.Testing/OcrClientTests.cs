using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Models;
using Regira.Office.OCR.Abstractions;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class OcrClientTests : OfficeClientTestsBase
{
    private readonly string _assetsDir;

    private IOcrService _service = null!;

    public OcrClientTests()
    {
        var testsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        _assetsDir = Path.Combine(testsDir, "Office.OCR.Testing", "Assets");
    }

    [SetUp]
    public void Setup()
    {
        _service = Services.GetRequiredService<IOcrService>();
    }

    [Test]
    public async Task Read_English_Image()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_assetsDir, "poem-en.jpg")) };

        var result = await _service.Read(file);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Text, Is.Not.Empty);
        Assert.That(result.Text!.ToLower(), Does.Contain("mother"));
    }

    [Test]
    public async Task Read_Dutch_Image()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_assetsDir, "poem-nl.jpg")) };

        var result = await _service.Read(file, "nl");

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Text, Is.Not.Empty);
    }

    [Test]
    public async Task Read_Returns_Text()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_assetsDir, "poem-en.jpg")) };

        var result = await _service.Read(file);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.Text, Is.Not.Empty);
        Assert.That(result.Text!.Length, Is.GreaterThan(10));
    }
}
