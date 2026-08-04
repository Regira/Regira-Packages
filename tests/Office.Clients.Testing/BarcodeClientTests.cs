using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Models;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class BarcodeClientTests : OfficeClientTestsBase
{
    private IBarcodeService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = Services.GetRequiredService<IBarcodeService>();
    }

    [TestCase("123456", BarcodeFormat.Code39)]
    [TestCase("This is a Code128 test", BarcodeFormat.Code128)]
    [TestCase("This is a DataMatrix test", BarcodeFormat.DataMatrix)]
    public async Task Create_Barcode(string content, BarcodeFormat format)
    {
        var input = new BarcodeInput { Content = content, Format = format };

        using var result = await _service.Create(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task Create_And_Read_Barcode()
    {
        var input = new BarcodeInput
        {
            Content = "Hello World",
            Format = BarcodeFormat.Code128
        };

        using var image = await _service.Create(input);
        var result = await _service.Read(image);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Contents?.FirstOrDefault(), Is.EqualTo(input.Content));
    }
}
