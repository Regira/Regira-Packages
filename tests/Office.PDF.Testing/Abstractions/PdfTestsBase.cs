using System.Text.RegularExpressions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Office.Models;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Defaults;
using Regira.Office.PDF.Models;
using Regira.Office.Utilities;
using Regira.Utilities;

namespace Office.PDF.Testing.Abstractions;

/// <summary>
/// Shared scenarios for the backends of PDF operations: text, merging, splitting, removing pages, page images and
/// images to PDF.
/// <para>
/// A backend fixture is marked <see cref="PdfFixtureAttribute"/> and gets every scenario whose
/// <see cref="NeedsAttribute">needs</see> its backend type implements. It declares the limits of a free edition
/// (<see cref="MaxPages"/>, <see cref="RenderedPages"/>), and overrides a scenario only where its backend documents a
/// different behaviour, restating the scenario's attributes when it does.
/// </para>
/// <para>
/// A scenario reads what it produced with <see cref="PdfFacts"/> or <see cref="ImageFacts"/>, never with the backend
/// under test, and saves it in <c>Assets/Output/{Backend}</c> under its own name, the facts beside it.
/// </para>
/// </summary>
/// <param name="backend">The backend; the scenarios it gets call it through the PDF interfaces they need.</param>
/// <param name="outputFolderName">The backend's folder under <c>Assets/Output</c>.</param>
public abstract class PdfTestsBase<TBackend>(TBackend backend, string outputFolderName) : PdfAssetsTestsBase(outputFolderName)
    where TBackend : class
{
    // a PDF point is 1/72 inch
    protected static readonly double PointsPerMm = DimensionsUtility.MmToPt(1f, DimensionsUtility.DPI.MAC_PPI);

    protected const string TwoPages = "sample.pdf";
    protected const string NinePages = "lorem-9-pages.pdf";
    private const string WithEmptyPages = "has-empty-pages.pdf";
    private static readonly string[] MergeInputs = Enumerable.Range(1, 6).Select(i => $"lorem-ipsum{i}.pdf").ToArray();

    // the text of sample.pdf, in the line breaks of the PDF
    private const string TwoPagesText = """
        A Simple PDF File
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
        Boring. More, a little more text. The end, and just as well.
        """;

    protected TBackend Backend { get; } = backend;

    /// <summary>The most pages a PDF the backend reads or writes may have; a scenario's inputs and results stay within it.</summary>
    protected virtual int MaxPages => int.MaxValue;
    /// <summary>The pages <c>ToImages</c> renders; a page past it comes out blank.</summary>
    protected virtual int RenderedPages => int.MaxValue;

    // the ink of a rendered page of text: sample.pdf's sparsest page has 1.2 %, a blank page none
    private const double Inked = 0.005;


    [Test, Needs(typeof(IPdfTextExtractor))]
    public async Task GetText_Reads_Every_Word_In_Order()
    {
        var text = await ((IPdfTextExtractor)Backend).GetText(ReadAsset(TwoPages));

        // the words and their order; how a backend breaks lines and spaces words differs
        Assert.That(Words(text), Is.EqualTo(Words(TwoPagesText)));
    }

    [Test, Needs(typeof(IPdfTextService))]
    public async Task GetTextPerPage_Reads_Each_Page()
    {
        var input = InputFacts(NinePages);

        var texts = await ((IPdfTextService)Backend).GetTextPerPage(ReadAsset(NinePages));

        // each page's own characters; the order a reader takes a page's lines in differs (a page number first or last)
        Assert.That(texts.Select(Characters), Is.EqualTo(input.PageTexts.Select(Characters)));
    }

    [Test, Needs(typeof(IPdfTextService))]
    public async Task RemoveEmptyPages_Keeps_The_Pages_With_Text()
    {
        var input = InputFacts(WithEmptyPages);
        Assert.That(input.PageTexts, Has.Some.Empty, "the input has empty pages");

        using var result = await ((IPdfTextService)Backend).RemoveEmptyPages(ReadAsset(WithEmptyPages));

        var output = await ReadPdf(result);
        Assert.That(output.PageTexts, Is.EqualTo(input.PageTexts.Where(text => text.Length > 0)));
    }

    [Test, Needs(typeof(IPdfTextService))]
    public async Task RemoveEmptyPages_Leaves_A_Pdf_Without_Them()
    {
        var input = InputFacts(NinePages);

        using var result = await ((IPdfTextService)Backend).RemoveEmptyPages(ReadAsset(NinePages));

        var output = await ReadPdf(result);
        Assert.That(output.PageTexts, Is.EqualTo(input.PageTexts));
    }


    [Test, Needs(typeof(IPdfMerger))]
    public async Task Merge_Keeps_Each_Pages_Text_In_Order()
    {
        var names = WithinMaxPages(MergeInputs);
        Assert.That(names, Has.Length.GreaterThan(2), "the merge has inputs to merge");
        var inputs = names.Select(InputFacts).ToList();

        using var merged = await ((IPdfMerger)Backend).Merge(names.Select(ReadAsset).ToList());

        var output = await ReadPdf(merged);
        var inputPages = inputs.SelectMany(input => input.Pages).ToList();
        Assert.That(output.PageTexts, Is.EqualTo(inputPages.Select(page => page.Text)));
        Assert.That(output.Pages.Select(Size), Is.EqualTo(inputPages.Select(Size)));
    }

    [Test, Needs(typeof(IPdfMerger))]
    public async Task Merge_Nothing_Gives_Null()
    {
        var merged = await ((IPdfMerger)Backend).Merge([]);

        Assert.That(merged, Is.Null);
    }


    [TestCase(TwoPages)]
    [TestCase(NinePages)]
    [Needs(typeof(IPdfSplitter))]
    public async Task GetPageCount_Counts_The_Pages(string filename)
    {
        var count = await ((IPdfSplitter)Backend).GetPageCount(ReadAsset(filename));

        Assert.That(count, Is.EqualTo(InputFacts(filename).PageCount));
    }

    /// <param name="ranges">Ranges of <c>lorem-9-pages.pdf</c>, comma-separated: <c>1-4</c> from page 1 to 4, <c>2</c> page 2 alone, <c>5-</c> from page 5 to the end</param>
    [TestCase("1-4,6-8")]
    [TestCase("2,5-")]
    [TestCase("1-9")]
    [Needs(typeof(IPdfSplitter))]
    public async Task Split_Keeps_The_Pages_Of_Each_Range(string ranges)
    {
        var input = InputFacts(NinePages);
        var splitRanges = Ranges(ranges);

        var parts = (await ((IPdfSplitter)Backend).Split(ReadAsset(NinePages), splitRanges)).ToList();

        Assert.That(parts, Has.Count.EqualTo(splitRanges.Length));
        for (var i = 0; i < parts.Count; i++)
        {
            using var part = parts[i];
            var output = await ReadPdf(part, $"-part{i + 1}");
            var expected = Pages(splitRanges[i], input.PageCount).Select(page => input.Pages[page - 1]).ToList();
            Assert.That(output.PageTexts, Is.EqualTo(expected.Select(page => page.Text)), $"part {i + 1}");
            Assert.That(output.Pages.Select(Size), Is.EqualTo(expected.Select(Size)), $"part {i + 1}");
        }
    }

    [Test, Needs(typeof(IPdfSplitter), typeof(IPdfMerger))]
    public async Task Split_Then_Merge_Keeps_The_Pages_In_Order()
    {
        var input = InputFacts(NinePages);
        var splitRanges = Ranges("2-4,6-7,9");

        var parts = (await ((IPdfSplitter)Backend).Split(ReadAsset(NinePages), splitRanges)).ToList();
        using var merged = await ((IPdfMerger)Backend).Merge(parts);
        parts.ForEach(part => part.Dispose());

        var output = await ReadPdf(merged);
        Assert.That(output.PageTexts, Is.EqualTo(new[] { 2, 3, 4, 6, 7, 9 }.Select(page => input.PageTexts[page - 1])));
    }

    /// <summary>Every backend checks a range against the document before it splits anything.</summary>
    [TestCase(0, 3)]
    [TestCase(8, 20)]
    [TestCase(5, 3)]
    [TestCase(10, null)]
    [Needs(typeof(IPdfSplitter))]
    public async Task Split_Refuses_A_Range_Outside_The_Document(int start, int? end)
    {
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            ((IPdfSplitter)Backend).Split(ReadAsset(NinePages), [new PdfSplitRange { Start = 2, End = 3 }, new PdfSplitRange { Start = start, End = end }]));
    }


    [TestCase("1")]
    [TestCase("5")]
    [TestCase("8")]
    [TestCase("9")]
    [TestCase("2,3")]
    [TestCase("5,3,3")]
    [Needs(typeof(IPdfEditor))]
    public async Task RemovePages_Keeps_The_Other_Pages_In_Order(string pages)
    {
        var input = InputFacts(NinePages);
        var toRemove = pages.Split(',').Select(int.Parse).ToArray();

        using var result = await ((IPdfEditor)Backend).RemovePages(ReadAsset(NinePages), toRemove);

        var output = await ReadPdf(result);
        var kept = Enumerable.Range(1, input.PageCount).Except(toRemove);
        Assert.That(output.PageTexts, Is.EqualTo(kept.Select(page => input.PageTexts[page - 1])));
    }

    [Test, Needs(typeof(IPdfEditor))]
    public async Task RemovePages_Ignores_Pages_That_Do_Not_Exist()
    {
        var input = InputFacts(NinePages);

        using var result = await ((IPdfEditor)Backend).RemovePages(ReadAsset(NinePages), [0, 3, 10]);

        var output = await ReadPdf(result);
        Assert.That(output.PageTexts, Is.EqualTo(input.PageTexts.Where((_, i) => i != 2)));
    }

    [Test, Needs(typeof(IPdfEditor))]
    public async Task RemovePages_Every_Page_Gives_Null()
    {
        var result = await ((IPdfEditor)Backend).RemovePages(ReadAsset(NinePages), Enumerable.Range(1, 9));

        Assert.That(result, Is.Null);
    }


    [Test, Needs(typeof(IPdfToImageService))]
    public async Task ToImages_Renders_Each_Page()
    {
        var input = InputFacts(NinePages);

        var images = await ((IPdfToImageService)Backend).ToImages(ReadAsset(NinePages));

        Assert.That(images, Has.Count.EqualTo(input.PageCount));
        for (var i = 0; i < images.Count; i++)
        {
            using var image = images[i];
            var facts = await ReadImage(image, $"-page{i + 1}");
            Assert.That(facts.Format, Is.EqualTo(PdfDefaults.ImageFormat), $"page {i + 1}");
            AssertFits(facts, input.Pages[i], PdfDefaults.ImageSize, $"page {i + 1}");
            // a free edition renders the first pages only, and the rest blank
            Assert.That(facts.Ink, i < RenderedPages ? Is.GreaterThan(Inked) : Is.LessThan(Inked / 10), $"page {i + 1}'s ink");
        }
    }

    [TestCase(480, 600)]
    [TestCase(600, 480)]
    [Needs(typeof(IPdfToImageService))]
    public async Task ToImages_Fit_The_Size_Either_Way_Round(int width, int height)
    {
        var input = InputFacts(TwoPages);
        var size = new ImageSize(width, height);

        var images = await ((IPdfToImageService)Backend).ToImages(ReadAsset(TwoPages), new PdfToImagesOptions { Size = size });

        Assert.That(images, Has.Count.EqualTo(input.PageCount));
        for (var i = 0; i < images.Count; i++)
        {
            using var image = images[i];
            AssertFits(await ReadImage(image, $"-page{i + 1}"), input.Pages[i], size, $"page {i + 1}");
        }
    }

    [TestCase(ImageFormat.Png, "image/png")]
    [TestCase(ImageFormat.Jpeg, "image/jpeg")]
    [Needs(typeof(IPdfToImageService))]
    public async Task ToImages_Are_The_Format_Asked_For(ImageFormat format, string contentType)
    {
        var images = await ((IPdfToImageService)Backend).ToImages(ReadAsset(TwoPages), new PdfToImagesOptions { Format = format });

        Assert.That(images, Has.Count.EqualTo(2));
        for (var i = 0; i < images.Count; i++)
        {
            using var image = images[i];
            var facts = await ReadImage(image, $"-page{i + 1}");
            Assert.That(facts.Format, Is.EqualTo(format), "the bytes");
            Assert.That(image.Format, Is.EqualTo(format), "the declared format");
            Assert.That(image.ContentType, Is.EqualTo(contentType));
            Assert.That(facts.Ink, Is.GreaterThan(Inked));
        }
    }


    /// <summary>
    /// Each image on a page of its own, of the input's format and orientation, inside the margins: centred
    /// horizontally, from the top margin.
    /// </summary>
    [TestCase("img-1.jpg,img-2.jpg,img-3.jpg,img-4.jpg", PageSize.A4, PageOrientation.Portrait)]
    [TestCase("lion.png,horse.png", PageSize.A4, PageOrientation.Landscape)]
    [TestCase("img-1.jpg,lion.png", PageSize.A5, PageOrientation.Portrait)]
    [Needs(typeof(IImagesToPdfService))]
    public virtual async Task ImagesToPdf_Puts_Each_Image_On_A_Page_Of_Its_Own(string images, PageSize format, PageOrientation orientation)
    {
        var input = new ImagesInput { Images = ReadImages(images), Format = format, Orientation = orientation };

        using var pdf = await ((IImagesToPdfService)Backend).ImagesToPdf(input);

        var output = await ReadPdf(pdf);
        var page = PageSizeUtility.GetPageSizeDimension(format, orientation: orientation);
        var margin = 10 * PointsPerMm;
        Assert.That(output.PageCount, Is.EqualTo(input.Images.Count));
        foreach (var (fact, i) in output.Pages.Select((fact, i) => (fact, i + 1)))
        {
            Assert.That(fact.Width, Is.EqualTo(page.Width * PointsPerMm).Within(1), $"page {i} width");
            Assert.That(fact.Height, Is.EqualTo(page.Height * PointsPerMm).Within(1), $"page {i} height");
            Assert.That(fact.Images, Has.Count.EqualTo(1), $"page {i} images");
            var bounds = fact.Images[0];
            Assert.That(bounds.Left, Is.GreaterThanOrEqualTo(margin - 0.5), $"page {i} image left");
            Assert.That(bounds.Right, Is.LessThanOrEqualTo(fact.Width - margin + 0.5), $"page {i} image right");
            Assert.That(bounds.Bottom, Is.LessThanOrEqualTo(fact.Height - margin + 0.5), $"page {i} image bottom");
            Assert.That(bounds.Top, Is.EqualTo(margin).Within(0.5), $"page {i} image top");
            Assert.That(bounds.Left, Is.EqualTo(fact.Width - bounds.Right).Within(0.5), $"page {i} image centred");
        }
    }

    [Test, Needs(typeof(IImagesToPdfService))]
    public async Task ImagesToPdf_Without_Images_Gives_Null()
    {
        var pdf = await ((IImagesToPdfService)Backend).ImagesToPdf(new ImagesInput());

        Assert.That(pdf, Is.Null);
    }


    protected static List<byte[]> ReadImages(string filenames)
        => filenames.Split(',').Select(filename => File.ReadAllBytes(InputPath(filename))).ToList();

    /// <summary>
    /// The size of a page image fits <paramref name="size"/> either way round: the page's shorter side the smaller
    /// dimension, its longer side the larger, one of them filled, at the page's aspect ratio.
    /// </summary>
    protected static void AssertFits(ImageFacts image, PageFact page, ImageSize size, string what)
    {
        var shortSide = Math.Min(size.Width, size.Height);
        var longSide = Math.Max(size.Width, size.Height);
        var (boxWidth, boxHeight) = page.Width <= page.Height ? (shortSide, longSide) : (longSide, shortSide);
        Assert.That(image.Width, Is.LessThanOrEqualTo(boxWidth + 1), $"{what} width");
        Assert.That(image.Height, Is.LessThanOrEqualTo(boxHeight + 1), $"{what} height");
        Assert.That(Math.Abs(image.Width - boxWidth) <= 1 || Math.Abs(image.Height - boxHeight) <= 1, Is.True,
            $"{what} is {image.Width} × {image.Height}, filling neither side of {boxWidth} × {boxHeight}");
        Assert.That((double)image.Width / image.Height, Is.EqualTo(page.Width / page.Height).Within(0.01), $"{what} aspect ratio");
    }

    /// <summary>The leading inputs whose pages together stay within <see cref="MaxPages"/>.</summary>
    private string[] WithinMaxPages(IEnumerable<string> filenames)
    {
        var pages = 0;
        return filenames.TakeWhile(filename => (pages += InputFacts(filename).PageCount) <= MaxPages).ToArray();
    }

    /// <summary>Split ranges written as <c>1-4,6,8-</c>.</summary>
    private static PdfSplitRange[] Ranges(string ranges)
        => ranges.Split(',')
            .Select(range => range.Split('-'))
            .Select(bounds => new PdfSplitRange
            {
                Start = int.Parse(bounds[0]),
                End = bounds.Length == 1 ? int.Parse(bounds[0]) : bounds[1].Length == 0 ? null : int.Parse(bounds[1])
            })
            .ToArray();

    private static IEnumerable<int> Pages(PdfSplitRange range, int pageCount)
        => Enumerable.Range(range.Start, (range.End ?? pageCount) - range.Start + 1);

    private static (double Width, double Height) Size(PageFact page) => (Math.Round(page.Width), Math.Round(page.Height));

    private static string[] Words(string text) => Regex.Split(text.Trim(), @"\s+");

    /// <summary>A text's characters apart from white space, in order of their code.</summary>
    private static string Characters(string text) => string.Concat(text.Where(c => !char.IsWhiteSpace(c)).Order());
}
