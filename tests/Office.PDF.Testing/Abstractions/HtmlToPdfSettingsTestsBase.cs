using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Regira.Office.Utilities;
using Regira.Utilities;
using UglyToad.PdfPig;
using Page = UglyToad.PdfPig.Content.Page;
using Word = UglyToad.PdfPig.Content.Word;

namespace Office.PDF.Testing.Abstractions;

/// <summary>
/// The <see cref="HtmlInput"/> page settings, read back from the PDF by the PdfPig library: page size, orientation,
/// margins, and a header and footer on every page.
/// </summary>
public abstract class HtmlToPdfSettingsTestsBase
{
    // a PDF point is 1/72 inch; HtmlInput measures in units of its DPI (96 by default)
    private static readonly double PointsPerMm = DimensionsUtility.MmToPt(1f, DimensionsUtility.DPI.MAC_PPI);
    private const double Tolerance = 1.5;

    protected abstract IHtmlToPdfService Backend { get; }
    protected abstract string OutputFolder { get; }

    [TestCase(PageSize.A4, PageOrientation.Portrait)]
    [TestCase(PageSize.A3, PageOrientation.Portrait)]
    [TestCase(PageSize.A5, PageOrientation.Landscape)]
    [TestCase(PageSize.A7, PageOrientation.Portrait)]
    public async Task Page_Takes_Format_And_Orientation(PageSize format, PageOrientation orientation)
    {
        var input = new HtmlInput { HtmlContent = "<p>Page</p>", Format = format, Orientation = orientation };

        using var pdf = await Create(input, $"{format}-{orientation}");

        var expected = PageSizeUtility.GetPageSizeDimension(format, orientation: orientation);
        var page = pdf.GetPage(1);
        Assert.That(page.Width, Is.EqualTo(expected.Width * PointsPerMm).Within(Tolerance));
        Assert.That(page.Height, Is.EqualTo(expected.Height * PointsPerMm).Within(Tolerance));
    }

    [Test]
    public async Task Default_Is_A4_Portrait_With_10mm_Margins()
    {
        using var pdf = await Create(new HtmlInput { HtmlContent = Probe("Corner") }, "defaults");

        var page = pdf.GetPage(1);
        Assert.That(page.Width, Is.EqualTo(210 * PointsPerMm).Within(Tolerance));
        Assert.That(page.Height, Is.EqualTo(297 * PointsPerMm).Within(Tolerance));
        AssertStartsAt(Word(page, "Corner"), page, 10, 10);
    }

    [TestCase(0f)]
    [TestCase(25f)]
    public async Task Content_Starts_At_The_Margins(float marginMm)
    {
        var input = new HtmlInput
        {
            HtmlContent = Probe("Corner"),
            Margins = DimensionsUtility.MmToPt(marginMm)
        };

        using var pdf = await Create(input, $"margin-{marginMm}mm");

        var page = pdf.GetPage(1);
        AssertStartsAt(Word(page, "Corner"), page, marginMm, marginMm);
    }

    [Test]
    public async Task Header_And_Footer_Sit_On_Every_Page_Between_Margins_And_Body()
    {
        const float margin = 15;
        const int headerHeight = 12;
        const int footerHeight = 8;
        var input = new HtmlInput
        {
            HtmlContent = ResetBody + string.Join("", Enumerable.Range(1, 3)
                .Select(i => $"<div style=\"{(i > 1 ? "page-break-before:always;" : "")}font-size:20px;line-height:1\">Body{i}</div>")),
            HeaderHtmlContent = ResetBody + "<span style=\"font-size:12px\">HeaderText</span>",
            HeaderHeight = headerHeight,
            FooterHtmlContent = ResetBody + "<span style=\"font-size:12px\">FooterText</span>",
            FooterHeight = footerHeight,
            Margins = DimensionsUtility.MmToPt(margin)
        };

        using var pdf = await Create(input, "header-footer");

        Assert.That(pdf.NumberOfPages, Is.EqualTo(3));
        foreach (var page in pdf.GetPages())
        {
            var header = Word(page, "HeaderText");
            var footer = Word(page, "FooterText");
            var body = Word(page, $"Body{page.Number}");

            // the header's band lies below the top margin, the body below the header's band
            AssertStartsAt(header, page, margin, margin);
            Assert.That(FromTop(header, page), Is.LessThan((margin + headerHeight) * PointsPerMm));
            AssertStartsAt(body, page, margin + headerHeight, margin);
            // the footer's band ends at the bottom margin
            Assert.That(FromTop(footer, page), Is.GreaterThanOrEqualTo(page.Height - (margin + footerHeight) * PointsPerMm - Tolerance));
            Assert.That(footer.BoundingBox.Bottom, Is.GreaterThanOrEqualTo(margin * PointsPerMm - Tolerance));
            Assert.That(footer.BoundingBox.Left, Is.EqualTo(margin * PointsPerMm).Within(Tolerance + 1));
        }
    }

    [Test]
    public async Task Header_Alone_Adds_No_Footer()
    {
        var input = new HtmlInput
        {
            HtmlContent = "<p>Body</p>",
            HeaderHtmlContent = "<span>HeaderText</span>"
        };

        using var pdf = await Create(input, "header-only");

        var words = pdf.GetPage(1).GetWords().Select(w => w.Text).ToArray();
        Assert.That(words, Is.EquivalentTo(new[] { "HeaderText", "Body" }));
    }


    private async Task<PdfDocument> Create(HtmlInput input, string name)
    {
        using var file = await Backend.Create(input);
        PdfTestHelper.AssertReadableWithoutRewind(file);
        var bytes = file.GetBytes()!;

        var outputDir = Path.Combine(AssemblyUtility.GetAssemblyDirectory()!, "../../../", "Assets", "Output", OutputFolder);
        Directory.CreateDirectory(outputDir);
        await File.WriteAllBytesAsync(Path.Combine(outputDir, $"settings-{name}.pdf"), bytes);

        return PdfDocument.Open(bytes);
    }

    // a browser's default style puts 8px around the body
    private const string ResetBody = "<style>body{margin:0}</style>";
    // one word at the content box's top-left corner, its glyphs filling the line
    private static string Probe(string word) => $"{ResetBody}<div style=\"font-size:20px;line-height:1\">{word}</div>";

    private static Word Word(Page page, string text)
    {
        var word = page.GetWords().FirstOrDefault(w => w.Text == text);
        Assert.That(word, Is.Not.Null, $"'{text}' is not on page {page.Number}: {string.Join(" ", page.GetWords().Select(w => w.Text))}");
        return word!;
    }

    private static double FromTop(Word word, Page page) => page.Height - word.BoundingBox.Top;

    // a glyph box starts a little right of and below its line box's corner
    private static void AssertStartsAt(Word word, Page page, double topMm, double leftMm)
    {
        Assert.That(word.BoundingBox.Left, Is.EqualTo(leftMm * PointsPerMm).Within(Tolerance + 1),
            $"'{word.Text}' left");
        Assert.That(FromTop(word, page), Is.InRange(topMm * PointsPerMm - Tolerance, topMm * PointsPerMm + 6),
            $"'{word.Text}' top");
    }
}
