using NUnit.Framework.Legacy;
using Office.PDF.Testing.Abstractions;
using Regira.Collections;
using Regira.Drawing.SkiaSharp.Services;
using Regira.IO.Extensions;
using Regira.IO.Storage.FileSystem;
using Regira.Office.PDF.DocNET;
using Regira.Office.PDF.Models;
using Regira.Utilities;

namespace Office.PDF.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class DocNETTests
{
    private readonly string _inputDir;
    private readonly string _outputDir;
    private readonly PdfManager _pdfService;

    public DocNETTests()
    {
        var imageService = new ImageService();
        _pdfService = new PdfManager(imageService);
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        var assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        _inputDir = Path.Combine(assetsDir, "Input");
        _outputDir = Path.Combine(assetsDir, "Output/DocNET");
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
        Assert.That(pdfText, Is.EqualTo(expectedText));
    }

    [Test]
    public async Task MergeDocs_By_Path()
    {
        var inputDocs = Enumerable.Range(1, 6)
            .Select(i => Path.Combine(_inputDir, $"lorem-ipsum{i}.pdf"))
            .ToArray();

        using var merged = _pdfService.Merge(inputDocs);

        var inputPageCounts = await Task.WhenAll(inputDocs.Select(doc => _pdfService.GetPageCount(File.ReadAllBytes(doc).ToBinaryFile())));
        var inputPageCount = inputPageCounts.Sum();
        var mergedPageCount = await _pdfService.GetPageCount(merged.ToBinaryFile());

        Assert.That(mergedPageCount, Is.EqualTo(inputPageCount));

        var outputPath = Path.Combine(_outputDir, "merged-by-path.pdf");
        await File.WriteAllBytesAsync(outputPath, merged.GetBytes()!);
    }
    [Test]
    public async Task MergeDocs_By_Stream()
    {
        var inputStreams = Enumerable.Range(1, 6)
            .Select(i => File.OpenRead(Path.Combine(_inputDir, $"lorem-ipsum{i}.pdf")).ToBinaryFile())
            .ToArray();

        using var merged = (await _pdfService.Merge(inputStreams))!;

        var inputPageCounts = await Task.WhenAll(inputStreams.Select(doc => _pdfService.GetPageCount(doc)));
        var inputPageCount = inputPageCounts.Sum();
        var mergedPageCount = await _pdfService.GetPageCount(merged.ToBinaryFile());

        Assert.That(mergedPageCount, Is.EqualTo(inputPageCount));

        var outputPath = Path.Combine(_outputDir, "merged-by-stream.pdf");
        await File.WriteAllBytesAsync(outputPath, merged.GetBytes()!);

        inputStreams.Dispose();
    }

    [Test]
    public Task Split_1to10_and_14to24()
        => PdfTestHelper.Split_Documents(_pdfService);

    [Test]
    public Task Merge_Split_Documents()
        => PdfTestHelper.Merge_Split_Documents(_pdfService, _pdfService);


    [Test]
    public async Task Remove_Empty_Pages()
    {
        var bf = (await FileSystemUtility.Parse(Path.Combine(_inputDir, "has-empty-pages.pdf")))!;
        var textsWithEmptyPages = await _pdfService.GetTextPerPage(bf);
        using var resultPdf = await _pdfService.RemoveEmptyPages(bf);
        var texts = await _pdfService.GetTextPerPage(resultPdf!.ToBinaryFile());
        Assert.That(texts, Is.Not.Empty);
        Assert.That(textsWithEmptyPages.Where(string.IsNullOrWhiteSpace), Is.Not.Empty);
        ClassicAssert.IsEmpty(texts.Where(string.IsNullOrWhiteSpace));
    }


    [Test]
    public Task ToImages()
        => PdfTestHelper.ToImages(_pdfService);

    [Test]
    public async Task JpegImagesToPdf()
    {
        var images = await Task.WhenAll(
            Enumerable.Range(1, 4)
                .Select(i => File.ReadAllBytesAsync(Path.Combine(_inputDir, $"img-{i}.jpg")))
        );

        var input = new ImagesInput { Images = images };
        using var pdf = await _pdfService.ImagesToPdf(input);

        var outputPath = Path.Combine(_outputDir, "jpg-images.pdf");
        await FileSystemUtility.SaveStream(outputPath, pdf.GetStream()!);
    }

    [Test]
    public async Task PngImagesToPdf()
    {
        var images = await Task.WhenAll("lion,horse".Split(",")
            .Select(img => File.ReadAllBytesAsync(Path.Combine(_inputDir, $"{img}.png")))
        );

        var input = new ImagesInput { Images = images };
        using var pdf = await _pdfService.ImagesToPdf(input);

        var outputPath = Path.Combine(_outputDir, "png-images.pdf");
        await FileSystemUtility.SaveStream(outputPath, pdf.GetStream()!);
    }
}