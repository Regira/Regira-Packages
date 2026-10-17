using Office.Barcodes.Testing.Abstractions;
using Regira.Office.Barcodes.Models;
using Regira.Office.Barcodes.ZXing;

namespace Office.Barcodes.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class ZXingBarcodeTests() : BarcodeTestsBase(new BarcodeService(), new BarcodeService(), "ZXing")
{
    [TestCase("123456", BarcodeFormat.Code39, "1to6")]
    [TestCase("TEST", BarcodeFormat.Code39, "test")]
    [TestCase("123456789", BarcodeFormat.Code93, "1to6")]
    [TestCase("This is a Code93 test", BarcodeFormat.Code93, "test")]
    [TestCase("1234567890", BarcodeFormat.Code128, "1to10")]
    [TestCase("This is a Code128 test", BarcodeFormat.Code128, "test")]
    [TestCase("12345678901234567890", BarcodeFormat.DataMatrix, "1to20")]
    [TestCase("This is a DataMatrix test", BarcodeFormat.DataMatrix, "test")]
    public override Task Create_Barcode(string content, BarcodeFormat format, string outputName)
    {
        return base.Create_Barcode(content, format, outputName);
    }

    [TestCase("123456", BarcodeFormat.Code39, new[] { 103, 50 })]
    [TestCase("123456789", BarcodeFormat.Code93, new[] { 118, 50 })]
    [TestCase("1234567890", BarcodeFormat.Code128, new[] { 101, 50 })]
    [TestCase("This is a DataMatrix test", BarcodeFormat.DataMatrix, new[] { 150, 150 })]
    public override Task Check_Dimensions(string content, BarcodeFormat format, int[] size)
    {
        return base.Check_Dimensions(content, format, size);
    }

    [Test]
    public override async Task TooLong_Expect_InputException()
    {
        await base.TooLong_Expect_InputException();
    }

    [TestCase("Code39.png", "123456")]
    [TestCase("Code39b.png", "012345678-")]
    [TestCase("Code93.jpg", "CODE93")]
    [TestCase("Code128.png", "1234567890")]
    [TestCase("Datamatrix.png", "This is a DataMatrix test")]
    public override async Task Read_Barcode(string inputImg, string expectedContent)
    {
        await base.Read_Barcode(inputImg, expectedContent);
    }

    [Test]
    public override Task Create_And_Read_Barcode()
    {
        return base.Create_And_Read_Barcode();
    }

    // A reader needs the quiet zone around the bars: without it an EAN or UPC does not decode
    [TestCase("5400000000478", BarcodeFormat.Ean13)]
    [TestCase("96385074", BarcodeFormat.Ean8)]
    [TestCase("036000291452", BarcodeFormat.Upca)]
    [TestCase("WD-10047", BarcodeFormat.Code128)]
    [TestCase("123456", BarcodeFormat.Code39)]
    [TestCase("This is a DataMatrix test", BarcodeFormat.DataMatrix)]
    public async Task Create_Then_Read_Returns_Content(string content, BarcodeFormat format)
    {
        using var barcode = await Writer!.Create(new BarcodeInput { Content = content, Format = format });

        var withFormat = await Reader!.Read(barcode, format);
        var withoutFormat = await Reader.Read(barcode);

        Assert.That(withFormat?.Contents?.FirstOrDefault(), Is.EqualTo(content));
        Assert.That(withoutFormat?.Contents?.FirstOrDefault(), Is.Not.Null);
    }

    // GS1-128: FNC1 (ñ) in front of the application identifiers, as the barcodes guide shows
    [Test]
    public async Task Create_Gs1_128_Then_Read_Returns_Digits()
    {
        const string sscc = "354123450000000017";
        using var barcode = await Writer!.Create(new BarcodeInput { Content = "ñ" + "00" + sscc, Format = BarcodeFormat.Code128 });

        var result = await Reader!.Read(barcode, BarcodeFormat.Code128);

        Assert.That(result?.Contents?.FirstOrDefault(), Is.EqualTo("00" + sscc));
    }
}