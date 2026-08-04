using NUnit.Framework.Legacy;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Utilities;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Exceptions;
using Regira.Office.Barcodes.Models;

namespace Office.Barcodes.Testing.Extensions;

public static class QRCodeTestExtensions
{
    public static async Task Create_QRCode(this IQRCodeWriter? writer, string input, string outputPath)
    {
        if (writer == null)
        {
            Assert.Ignore("Writing not supported");
            return;
        }

        using var barCodeImg = await writer.Create(input);
        await barCodeImg.SaveAs(outputPath);
        ClassicAssert.IsNotNull(barCodeImg.GetBytes());
        Assert.That(barCodeImg.GetLength() > 0, Is.True);
    }
    public static async Task Check_Dimensions(this IQRCodeWriter? writer, string content, int size, string outputDirectory)
    {
        if (writer == null)
        {
            Assert.Ignore("Writing not supported");
            return;
        }

        var input = new QRCodeInput
        {
            Content = content,
            Size = size
        };
        using var barCodeImg = await writer.Create(input);
        await barCodeImg.SaveAs(Path.Combine(outputDirectory, $"barcode-{input.Size.Width}x{input.Size.Height}-{writer.GetType().Name}.jpg"));
        ClassicAssert.IsNotNull(barCodeImg.GetBytes());
        Assert.That(barCodeImg.GetLength() > 0, Is.True);
        Assert.That(barCodeImg.Size?.Width, Is.EqualTo(input.Size.Width));
    }
    public static async Task TooLong_Expect_InputException(this IQRCodeWriter? writer)
    {
        if (writer == null)
        {
            Assert.Ignore("Writing not supported");
            return;
        }

        var input = Convert.ToBase64String(Enumerable.Range(0, 2500).Select((_, i) => (byte)(i % 8)).ToArray());
        Assert.ThrowsAsync<InputException>(async () =>
        {
            await writer.Create(input);
        });
    }

    public static async Task Read_QRCode(this IQRCodeReader? reader, string inputPath, string expectedContent)
    {
        if (reader == null)
        {
            Assert.Ignore("Reading not supported");
            return;
        }

        var inputImg = new BinaryFileItem(inputPath).ToImageFile();

        var result = await reader.Read(inputImg);
        var content = string.Join(Environment.NewLine, result?.Contents!);
        Assert.That(content, Is.EqualTo(expectedContent));
    }

    public static Task Create_And_Read_QRCode(this IQRCodeService service)
        => service.Create_And_Read_QRCode(service);
    public static async Task Create_And_Read_QRCode(this IQRCodeWriter? writer, IQRCodeReader? reader)
    {
        if (reader == null)
        {
            Assert.Ignore("Reading not supported");
            return;
        }

        var input = "This is a test";
        var inputBytes = writer != null ? await writer.Create(input) : null;
        var result = await reader.Read(inputBytes!);
        var content = result?.Contents?.FirstOrDefault();
        Assert.That(content, Is.EqualTo(input));
    }
}