using NUnit.Framework.Legacy;
using Regira.Drawing.SkiaSharp.Services;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Utilities;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Exceptions;
using Regira.Office.Barcodes.Models;
using Regira.Utilities;

namespace Office.Barcodes.Testing.Abstractions;

public abstract class QRCodeTestsBase
{
    protected readonly IQRCodeWriter? QRWriter;
    protected readonly IQRCodeReader? QRReader;
    protected readonly string InputDir;
    protected readonly string OutputDir;
    protected readonly ImageService ImageService;
    protected QRCodeTestsBase(IQRCodeService service, string outputFolderName)
        : this(service, service, outputFolderName)
    {
    }
    protected QRCodeTestsBase(IQRCodeWriter? writer, IQRCodeReader? reader, string outputFolderName)
    {
        QRWriter = writer;
        QRReader = reader;
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        var assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        InputDir = Path.Combine(assetsDir, "Input", "QR");
        OutputDir = Path.Combine(assetsDir, "Output", "QR", outputFolderName);
        Directory.CreateDirectory(OutputDir);

        ImageService = new ImageService();
    }


    public virtual async Task Create_QRCode(string input, string outputName)
    {
        if (QRWriter == null)
        {
            Assert.Ignore("Writing not supported");
            return;
        }

        using var barCodeImg = await QRWriter.Create(input);
        await barCodeImg.SaveAs(Path.Combine(OutputDir, $"{outputName}.jpg"));
        ClassicAssert.IsNotNull(barCodeImg.GetBytes());
        Assert.That(barCodeImg.GetLength() > 0, Is.True);
    }
    public virtual async Task Check_Dimensions(string content, int size)
    {
        if (QRWriter == null)
        {
            Assert.Ignore("Writing not supported");
            return;
        }

        var input = new QRCodeInput
        {
            Content = content,
            Size = size
        };
        using var barCodeImg = await QRWriter.Create(input);
        await barCodeImg.SaveAs(Path.Combine(OutputDir, $"qrcode-{input.Size.Width}x{input.Size.Height}.jpg"));
        ClassicAssert.IsNotNull(barCodeImg.GetBytes());
        Assert.That(barCodeImg.GetLength() > 0, Is.True);
        Assert.That(barCodeImg.Size?.Width, Is.EqualTo(input.Size.Width));
    }
    public virtual async Task TooLong_Expect_InputException()
    {
        if (QRWriter == null)
        {
            Assert.Ignore("Writing not supported");
            return;
        }

        var input = Convert.ToBase64String(Enumerable.Range(0, 2500).Select((_, i) => (byte)(i % 8)).ToArray());
        Assert.ThrowsAsync<InputException>(async () =>
        {
            await QRWriter.Create(input);
        });
    }

    public virtual async Task Read_QRCode(string filename, string expectedContent)
    {
        if (QRReader == null)
        {
            Assert.Ignore("Reading not supported");
            return;
        }

        var inputImg = new BinaryFileItem(Path.Combine(InputDir, filename)).ToImageFile();

        var result = await QRReader.Read(inputImg);
        var content = string.Join(Environment.NewLine, result?.Contents!);
        Assert.That(content, Is.EqualTo(expectedContent));
    }

    public virtual async Task Create_And_Read_QRCode()
    {
        if (QRReader == null)
        {
            Assert.Ignore("Reading not supported");
            return;
        }

        var input = "This is a test";
        var qrImage = QRWriter != null ? await QRWriter.Create(input) : null;
        await qrImage!.SaveAs(Path.Combine(OutputDir, $"qrcode-{input.Length}.jpg"));
        var result = await QRReader.Read(qrImage!);
        var content = result?.Contents?.FirstOrDefault();
        Assert.That(content, Is.EqualTo(input));
    }
}
