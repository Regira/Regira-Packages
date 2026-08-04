using Office.Barcodes.Testing.Abstractions;
using Regira.Office.Barcodes.Spire;

namespace Office.Barcodes.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class SpireQRCodeTests() : QRCodeTestsBase(new QRCodeService(), "Spire")
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
        Assert.Ignore("Not supported");
        //await base.TooLong_Expect_InputException();
    }

    [TestCase("britannica.jpg", "http://itunes.apple.com/us/app/encyclopaedia-britannica/id447919187?mt=8")]
    [TestCase("cyberciti.png", "https://www.cyberciti.biz/")]
    [TestCase("wikipedia.png", "http://en.m.wikipedia.org")]
    [TestCase("wikipedia-picture.jpg", "http://en.m.wikipedia.org")]
    [TestCase("collection-picture.jpg", "http://itunes.apple.com/us/app/encyclopaedia-britannica/id447919187?mt=8\r\nhttp://en.m.wikipedia.org\r\nhttps://www.cyberciti.biz/")]
    public override async Task Read_QRCode(string inputImg, string expectedContent)
    {
        Assert.Ignore("Not supported in free version");
        await base.Read_QRCode(inputImg, expectedContent);
    }

    [Test]
    public override Task Create_And_Read_QRCode()
    {
        Assert.Ignore("Not supported in free version");
        //return base.Create_And_Read_QRCode();
        return Task.CompletedTask;
    }
}