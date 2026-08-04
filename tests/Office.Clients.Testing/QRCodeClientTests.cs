using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Models;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class QRCodeClientTests : OfficeClientTestsBase
{
    private IQRCodeService _service = null!;

    [SetUp]
    public void Setup()
    {
        _service = Services.GetRequiredService<IQRCodeService>();
    }

    [TestCase("Hello World")]
    [TestCase("https://example.com")]
    public async Task Create_QRCode(string content)
    {
        var input = new QRCodeInput { Content = content };

        using var result = await _service.Create(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task Create_And_Read_QRCode()
    {
        const string content = "Hello from QRCode!";
        var input = new QRCodeInput { Content = content };

        using var image = await _service.Create(input);
        var result = await _service.Read(image);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.Contents?.FirstOrDefault(), Is.EqualTo(content));
    }
}
