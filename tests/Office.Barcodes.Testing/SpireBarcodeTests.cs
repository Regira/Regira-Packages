using Office.Barcodes.Testing.Abstractions;
using Regira.Office.Barcodes.Models;
using Regira.Office.Barcodes.Spire;

namespace Office.Barcodes.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class SpireBarcodeTests() : BarcodeTestsBase(new BarcodeService(), new BarcodeService(), "Spire")
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

    [TestCase("123456", BarcodeFormat.Code39, new[] { 150, 50 })]
    [TestCase("123456789", BarcodeFormat.Code93, new[] { 150, 50 })]
    [TestCase("1234567890", BarcodeFormat.Code128, new[] { 150, 50 })]
    [TestCase("This is a DataMatrix test", BarcodeFormat.DataMatrix, new[] { 150, 150 })]
    public override Task Check_Dimensions(string content, BarcodeFormat format, int[] size)
    {
        return base.Check_Dimensions(content, format, size);
    }

    //[Test]
    //public override void TooLong_Expect_InputException()
    //{
    //     Assert.Ignore("Spire just generates an invalid barcode...");
    //}

    [TestCase("Code39.png", "123456")]
    [TestCase("Code39b.png", "012345678-")]
    [TestCase("Code93.jpg", "CODE93")]
    [TestCase("Code128.png", "1234567890")]
    //Not supported in free version:
    //[TestCase("Datamatrix.png", "This is a DataMatrix test")]
    public override async Task Read_Barcode(string inputImg, string expectedContent)
    {
        await base.Read_Barcode(inputImg, expectedContent);
    }

    [Test]
    public override Task Create_And_Read_Barcode()
    {
        return base.Create_And_Read_Barcode();
    }
}