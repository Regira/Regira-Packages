using Office.Barcodes.Testing.Abstractions;
using Regira.Office.Barcodes.QRCoder;

[assembly: Parallelizable(ParallelScope.Fixtures)]

namespace Office.Barcodes.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Default)]
public class QRCoderTests() : QRCodeTestsBase(new QRCodeWriter(), null, "QR-Coder")
{
    [TestCase("test", "test-lowercase")]
    [TestCase("TEST", "test-uppercase")]
    [TestCase("https://github.com/micjahn/ZXing.Net", "zxing")]
    [TestCase("https://github.com/codebude/QRCoder/", "qrcoder")]
    [TestCase("https://www.codeproject.com/Articles/1250071/QR-Code-Encoder-and-Decoder-NET-Framework-Standard", "uzi-granot")]
    [TestCase("https://www.e-iceblue.com/Tutorials/Spire.Barcode/Spire.Barcode-Program-Guide/Create-a-QR-Code-in-C.html", "spire")]
    [TestCase("https://en.wikipedia.org/", "Wikipedia")]
    public override Task Create_QRCode(string input, string outputName)
    {
        return base.Create_QRCode(input, outputName);
    }

    [TestCase("https://en.wikipedia.org/wiki/QR_code", 150)]
    public override Task Check_Dimensions(string content, int size)
    {
        return base.Check_Dimensions(content, size);
    }

    [Test]
    public override async Task TooLong_Expect_InputException()
    {
        await base.TooLong_Expect_InputException();
    }
}