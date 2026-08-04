using Office.Barcodes.Testing.Extensions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.UziGranot;
using Regira.Utilities;

namespace Office.Barcodes.Testing;


[TestFixture]
[Parallelizable(ParallelScope.Self)]// ParallelScope.All causes Read_QRCode to fail when running with other tests...
public class UziGranotTests
{
    private readonly IQRCodeService _qrService;
    protected readonly string InputDir;
    protected readonly string OutputDir;
    public UziGranotTests()
    {
        _qrService = new QRCodeService();
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        var assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        InputDir = Path.Combine(assetsDir, "Input", "QR");
        OutputDir = Path.Combine(assetsDir, "Output", "QR", "Uzi-Granot");
        Directory.CreateDirectory(OutputDir);
    }


    [TestCase("test", "test-lowercase")]
    [TestCase("TEST", "test-uppercase")]
    [TestCase("https://github.com/micjahn/ZXing.Net", "zxing")]
    [TestCase("https://github.com/codebude/QRCoder/", "qrcoder")]
    [TestCase("https://www.codeproject.com/Articles/1250071/QR-Code-Encoder-and-Decoder-NET-Framework-Standard", "uzi-granot")]
    [TestCase("https://www.e-iceblue.com/Tutorials/Spire.Barcode/Spire.Barcode-Program-Guide/Create-a-QR-Code-in-C.html", "spire")]
    [TestCase("https://en.wikipedia.org/", "Wikipedia")]
    public Task Create_QRCode(string input, string outputName)
        => _qrService.Create_QRCode(input, Path.Combine(OutputDir, $"{outputName}.jpg"));

    [TestCase("https://en.wikipedia.org/wiki/QR_code", 150)]
    public Task Check_Dimensions(string content, int size)
        => _qrService.Check_Dimensions(content, size, OutputDir);

    [Test]
    public Task TooLong_Expect_InputException()
        => _qrService.TooLong_Expect_InputException();

    [TestCase("britannica.jpg", "http://itunes.apple.com/us/app/encyclopaedia-britannica/id447919187?mt=8")]
    [TestCase("cyberciti.png", "https://www.cyberciti.biz/")]
    //[TestCase("wikipedia.png", "http://en.m.wikipedia.org")]// bad image ??? -> does work with a screenshot from this image (see below) ...
    [TestCase("wikipedia-picture.jpg", "http://en.m.wikipedia.org")]
    [TestCase("collection-picture.jpg", "http://itunes.apple.com/us/app/encyclopaedia-britannica/id447919187?mt=8\r\nhttp://en.m.wikipedia.org\r\nhttps://www.cyberciti.biz/")]
    public Task Read_QRCode(string inputImg, string expectedContent)
        => _qrService.Read_QRCode(Path.Combine(InputDir, inputImg), expectedContent);

    [Test]
    public Task Create_And_Read_QRCode()
        => _qrService.Create_And_Read_QRCode();
}