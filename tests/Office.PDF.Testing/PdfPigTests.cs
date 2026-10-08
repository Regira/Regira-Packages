using System.Text.RegularExpressions;
using NUnit.Framework.Legacy;
using Office.PDF.Testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.IO.Extensions;
using Regira.IO.Storage.FileSystem;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Dimensions;
using Regira.Office.Models;
using Regira.Office.PDF.Models;
using Regira.Office.PDF.PdfPig;
using Regira.Utilities;
using UglyToad.PdfPig;

namespace Office.PDF.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PdfPigTests
{
    private readonly string _inputDir;
    private readonly string _outputDir;
    private readonly PdfService _pdfService;

    public PdfPigTests()
    {
        _pdfService = new PdfService(new ImageService());
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        var assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        _inputDir = Path.Combine(assetsDir, "Input");
        _outputDir = Path.Combine(assetsDir, "Output/PdfPig");
        Directory.CreateDirectory(_outputDir);
    }

    [Test]
    public async Task ReadText()
    {
        var expectedText = @"A Simple PDF File
 This is a small demonstration .pdf file -
 just for use in the Virtual Mechanics tutorials. More text. And more
 text. And more text. And more text. And more text.
 And more text. And more text. And more text. And more text. And more
 text. And more text. Boring, zzzzz. And more text. And more text. And
 more text. And more text. And more text. And more text. And more text.
 And more text. And more text.
 And more text. And more text. And more text. And more text. And more
 text. And more text. And more text. Even more. Continued on page 2 ...
Simple PDF File 2
 ...continued from page 1. Yet more text. And more text. And more text.
 And more text. And more text. And more text. And more text. And more
 text. Oh, how boring typing this stuff. But not as boring as watching
 paint dry. And more text. And more text. And more text. And more text.
 Boring. More, a little more text. The end, and just as well.";
        await using var pdfStream = File.OpenRead(Path.Combine(_inputDir, "sample.pdf"));
        var pdfText = await _pdfService.GetText(pdfStream.ToBinaryFile());
        // the words and their order; how a backend breaks lines and spaces words differs
        Assert.That(Words(pdfText), Is.EqualTo(Words(expectedText)));
    }

    [Test]
    public async Task MergeDocs()
    {
        var inputs = Enumerable.Range(1, 6)
            .Select(i => File.ReadAllBytes(Path.Combine(_inputDir, $"lorem-ipsum{i}.pdf")).ToBinaryFile())
            .ToArray();

        using var merged = (await _pdfService.Merge(inputs))!;

        var inputTexts = new List<string>();
        foreach (var input in inputs)
        {
            inputTexts.AddRange(await _pdfService.GetTextPerPage(input));
        }
        var mergedTexts = await _pdfService.GetTextPerPage(merged.ToBinaryFile());

        Assert.That(mergedTexts, Is.EqualTo(inputTexts));
        PdfTestHelper.AssertReadableWithoutRewind(merged);

        await File.WriteAllBytesAsync(Path.Combine(_outputDir, "merged.pdf"), merged.GetBytes()!);
    }

    [Test]
    public async Task Merge_Nothing_Gives_Null()
    {
        var merged = await _pdfService.Merge([]);

        Assert.That(merged, Is.Null);
    }

    [Test]
    public Task Split_1to10_and_14to24()
        => PdfTestHelper.Split_Documents(_pdfService);

    [Test]
    public async Task Split_Keeps_The_Pages_Text()
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(_inputDir, "lorem-9-pages.pdf")))!;
        var textsPerPage = await _pdfService.GetTextPerPage(bf);

        var parts = (await _pdfService.Split(bf, [new PdfSplitRange { Start = 2, End = 3 }, new PdfSplitRange { Start = 8 }])).ToArray();

        Assert.That(await _pdfService.GetTextPerPage(parts[0]), Is.EqualTo(new[] { textsPerPage[1], textsPerPage[2] }));
        Assert.That(await _pdfService.GetTextPerPage(parts[1]), Is.EqualTo(new[] { textsPerPage[7], textsPerPage[8] }));
    }

    [Test]
    public Task Merge_Split_Documents()
        => PdfTestHelper.Merge_Split_Documents(_pdfService, _pdfService);


    [TestCase(new[] { 1 }, new[] { 2, 3, 4, 5, 6, 7, 8, 9 })]
    [TestCase(new[] { 5 }, new[] { 1, 2, 3, 4, 6, 7, 8, 9 })]
    [TestCase(new[] { 8 }, new[] { 1, 2, 3, 4, 5, 6, 7, 9 })]
    [TestCase(new[] { 9 }, new[] { 1, 2, 3, 4, 5, 6, 7, 8 })]
    [TestCase(new[] { 2, 3 }, new[] { 1, 4, 5, 6, 7, 8, 9 })]
    [TestCase(new[] { 5, 3, 3 }, new[] { 1, 2, 4, 6, 7, 8, 9 })]
    public async Task Remove_Pages(int[] pagesToRemove, int[] expectedPages)
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(_inputDir, "lorem-9-pages.pdf")))!;
        var textsPerPage = await _pdfService.GetTextPerPage(bf);

        using var resultPdf = await _pdfService.RemovePages(bf, pagesToRemove);

        PdfTestHelper.AssertReadableWithoutRewind(resultPdf);
        var resultTexts = await _pdfService.GetTextPerPage(resultPdf!.ToBinaryFile());
        Assert.That(resultTexts, Is.EqualTo(expectedPages.Select(page => textsPerPage[page - 1])));
    }

    [Test]
    public async Task Remove_Every_Page_Gives_Null()
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(_inputDir, "lorem-9-pages.pdf")))!;

        var resultPdf = await _pdfService.RemovePages(bf, Enumerable.Range(1, 9));

        Assert.That(resultPdf, Is.Null);
    }

    [Test]
    public async Task Remove_Empty_Pages()
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(_inputDir, "has-empty-pages.pdf")))!;
        var textsWithEmptyPages = await _pdfService.GetTextPerPage(bf);
        using var resultPdf = await _pdfService.RemoveEmptyPages(bf);
        PdfTestHelper.AssertReadableWithoutRewind(resultPdf);
        var texts = await _pdfService.GetTextPerPage(resultPdf!.ToBinaryFile());
        Assert.That(texts, Is.Not.Empty);
        Assert.That(textsWithEmptyPages.Where(string.IsNullOrWhiteSpace), Is.Not.Empty);
        ClassicAssert.IsEmpty(texts.Where(string.IsNullOrWhiteSpace));
    }


    [Test]
    public Task ToImages()
        => PdfTestHelper.ToImages(_pdfService);

    [TestCase(480, 600)]
    [TestCase(600, 480)]
    public async Task ToImages_Fit_The_Size_Either_Way_Round(int width, int height)
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(_inputDir, "sample.pdf")))!;

        var images = await _pdfService.ToImages(bf, new PdfToImagesOptions { Size = new ImageSize(width, height) });

        // sample.pdf has portrait pages: the short side fits 480, the long side 600, whichever way round the size is given
        Assert.That(images, Has.Count.EqualTo(2));
        foreach (var image in images)
        {
            Assert.That(image.Size!.Value.Width, Is.LessThanOrEqualTo(480));
            Assert.That(image.Size!.Value.Height, Is.LessThanOrEqualTo(600));
            Assert.That(image.Size!.Value.Width == 480 || image.Size!.Value.Height == 600, Is.True);
        }
    }

    [TestCase(ImageFormat.Png, "image/png", new byte[] { 0x89, 0x50, 0x4E, 0x47 })]
    [TestCase(ImageFormat.Jpeg, "image/jpeg", new byte[] { 0xFF, 0xD8, 0xFF })]
    public async Task ToImages_Are_The_Format_Asked_For(ImageFormat format, string contentType, byte[] signature)
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(_inputDir, "sample.pdf")))!;

        var images = await _pdfService.ToImages(bf, new PdfToImagesOptions { Format = format });

        Assert.That(images, Has.Count.EqualTo(2));
        foreach (var image in images)
        {
            Assert.That(image.Format, Is.EqualTo(format));
            Assert.That(image.ContentType, Is.EqualTo(contentType));
            Assert.That(image.GetBytes()!.Take(signature.Length), Is.EqualTo(signature));
        }
    }

    [Test]
    public async Task JpegImagesToPdf()
    {
        var images = await Task.WhenAll(
            Enumerable.Range(1, 4)
                .Select(i => File.ReadAllBytesAsync(Path.Combine(_inputDir, $"img-{i}.jpg")))
        );

        var input = new ImagesInput { Images = images };
        using var pdf = (await _pdfService.ImagesToPdf(input))!;
        PdfTestHelper.AssertReadableWithoutRewind(pdf);
        AssertImagePages(pdf.GetBytes()!, images.Length, 595, 842);

        await FileSystemUtility.SaveStream(Path.Combine(_outputDir, "jpg-images.pdf"), pdf.GetStream()!);
    }

    [Test]
    public async Task PngImagesToPdf_Landscape()
    {
        var images = await Task.WhenAll("lion,horse".Split(",")
            .Select(img => File.ReadAllBytesAsync(Path.Combine(_inputDir, $"{img}.png")))
        );

        var input = new ImagesInput { Images = images, Orientation = PageOrientation.Landscape };
        using var pdf = (await _pdfService.ImagesToPdf(input))!;
        PdfTestHelper.AssertReadableWithoutRewind(pdf);
        AssertImagePages(pdf.GetBytes()!, images.Length, 842, 595);

        await FileSystemUtility.SaveStream(Path.Combine(_outputDir, "png-images.pdf"), pdf.GetStream()!);
    }

    [Test]
    public async Task No_Images_Gives_Null()
    {
        var pdf = await _pdfService.ImagesToPdf(new ImagesInput());

        Assert.That(pdf, Is.Null);
    }


    private static void AssertImagePages(byte[] pdf, int expectedPageCount, double expectedWidth, double expectedHeight)
    {
        using var document = PdfDocument.Open(pdf);
        Assert.That(document.NumberOfPages, Is.EqualTo(expectedPageCount));
        foreach (var page in document.GetPages())
        {
            Assert.That(page.Width, Is.EqualTo(expectedWidth).Within(1));
            Assert.That(page.Height, Is.EqualTo(expectedHeight).Within(1));
            var bounds = page.GetImages().Single().BoundingBox;
            // clear of the default 10 mm margins (28.3 pt), centred horizontally, from the top margin
            Assert.That(bounds.Left, Is.GreaterThanOrEqualTo(28));
            Assert.That(bounds.Right, Is.LessThanOrEqualTo(expectedWidth - 28));
            Assert.That(bounds.Bottom, Is.GreaterThanOrEqualTo(28));
            Assert.That(bounds.Left, Is.EqualTo(expectedWidth - bounds.Right).Within(1));
            Assert.That(bounds.Top, Is.EqualTo(expectedHeight - 28.3).Within(1));
        }
    }

    private static string[] Words(string text) => Regex.Split(text.Trim(), @"\s+");
}
