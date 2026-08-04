using Microsoft.Extensions.DependencyInjection;
using Office.Clients.Testing.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Dimensions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;

namespace Office.Clients.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class PdfClientTests : OfficeClientTestsBase
{
    private readonly string _inputDir;
    private readonly string _outputDir;

    private IHtmlToPdfService _htmlToPdf = null!;
    private IImagesToPdfService _imagesToPdf = null!;
    private IPdfMerger _merger = null!;
    private IPdfSplitter _splitter = null!;
    private IPdfTextExtractor _textExtractor = null!;
    private IPdfToImageService _toImages = null!;

    public PdfClientTests()
    {
        var testsDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../../"));
        _inputDir = Path.Combine(testsDir, "Office.PDF.Testing", "Assets", "Input");
        _outputDir = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "../../../Assets/Output"));
        Directory.CreateDirectory(_outputDir);
    }

    [SetUp]
    public void Setup()
    {
        _htmlToPdf = Services.GetRequiredService<IHtmlToPdfService>();
        _imagesToPdf = Services.GetRequiredService<IImagesToPdfService>();
        _merger = Services.GetRequiredService<IPdfMerger>();
        _splitter = Services.GetRequiredService<IPdfSplitter>();
        _textExtractor = Services.GetRequiredService<IPdfTextExtractor>();
        _toImages = Services.GetRequiredService<IPdfToImageService>();
    }

    [Test]
    public async Task HtmlToPdf()
    {
        var input = new HtmlInput { HtmlContent = "<html><body><h1>Hello World</h1><p>Integration test.</p></body></html>" };

        using var result = await _htmlToPdf.Create(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task HtmlToPdf_WithHeaderAndFooter()
    {
        var input = new HtmlInput
        {
            HtmlContent = "<html><body><p>Page content</p></body></html>",
            HeaderHtmlContent = "<div style='text-align:center;font-size:10px'>Header</div>",
            HeaderHeight = 20,
            FooterHtmlContent = "<div style='text-align:center;font-size:10px'>Footer</div>",
            FooterHeight = 15
        };

        using var result = await _htmlToPdf.Create(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task ImagesToPdf()
    {
        var imageBytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "horse.png"));
        var input = new ImagesInput();
        input.Images.Add(imageBytes);

        using var result = await _imagesToPdf.ImagesToPdf(input);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task GetPageCount()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "lorem-9-pages.pdf")) };

        var count = await _splitter.GetPageCount(file);

        Assert.That(count, Is.EqualTo(9));
    }

    [Test]
    public async Task GetText()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "lorem-ipsum1.pdf")) };

        var text = await _textExtractor.GetText(file);

        Assert.That(text, Is.Not.Null);
        Assert.That(text, Is.Not.Empty);
    }

    [Test]
    public async Task Split_Pdf()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "lorem-24-pages.pdf")) };
        var ranges = new[]
        {
            new PdfSplitRange { Start = 1, End = 5 },
            new PdfSplitRange { Start = 6, End = 10 }
        };

        var parts = (await _splitter.Split(file, ranges)).ToList();

        Assert.That(parts, Has.Count.EqualTo(2));
    }

    [Test]
    public async Task Merge_Pdfs()
    {
        var pdf1 = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "lorem-ipsum1.pdf")) };
        var pdf2 = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "lorem-ipsum2.pdf")) };

        using var result = await _merger.Merge([pdf1, pdf2]);

        Assert.That(result, Is.Not.Null);
        Assert.That(result!.GetLength() > 0, Is.True);
    }

    [Test]
    public async Task ToImages()
    {
        var file = new BinaryFileItem { Bytes = await File.ReadAllBytesAsync(Path.Combine(_inputDir, "sample.pdf")) };
        ImageSize size = new[] { 480, 600 };

        var images = await _toImages.ToImages(file, new PdfToImagesOptions { Size = size });

        Assert.That(images, Is.Not.Null);
        Assert.That(images, Is.Not.Empty);
    }
}
