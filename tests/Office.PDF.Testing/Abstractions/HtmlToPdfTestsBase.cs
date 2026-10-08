using Regira.Office.Models;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Regira.Office.Utilities;
using Regira.Serializing.Newtonsoft.Json;
using Regira.Utilities;
using Regira.Web.HTML;

namespace Office.PDF.Testing.Abstractions;

/// <summary>
/// Shared scenarios for every <see cref="IHtmlToPdfService"/>: the <see cref="HtmlInput"/> page settings — page size,
/// orientation, margins, and a header and footer on every page — and a template filled with parameters.
/// <para>
/// A scenario reads what it produced with <see cref="PdfFacts"/>, never with the backend under test, and saves it in
/// <c>Assets/Output/{Backend}</c> under its own name, the facts beside it.
/// </para>
/// </summary>
/// <param name="backend">The backend.</param>
/// <param name="outputFolderName">The backend's folder under <c>Assets/Output</c>.</param>
public abstract class HtmlToPdfTestsBase(IHtmlToPdfService backend, string outputFolderName) : PdfAssetsTestsBase(outputFolderName)
{
    // a PDF point is 1/72 inch; HtmlInput measures in units of its DPI (96 by default)
    private static readonly double PointsPerMm = DimensionsUtility.MmToPt(1f, DimensionsUtility.DPI.MAC_PPI);
    private const double Tolerance = 1.5;

    protected IHtmlToPdfService Backend { get; } = backend;

    [TestCase(PageSize.A4, PageOrientation.Portrait)]
    [TestCase(PageSize.A3, PageOrientation.Portrait)]
    [TestCase(PageSize.A5, PageOrientation.Landscape)]
    [TestCase(PageSize.A7, PageOrientation.Portrait)]
    public async Task Page_Takes_Format_And_Orientation(PageSize format, PageOrientation orientation)
    {
        var input = new HtmlInput { HtmlContent = "<p>Page</p>", Format = format, Orientation = orientation };

        var pdf = await Create(input);

        var expected = PageSizeUtility.GetPageSizeDimension(format, orientation: orientation);
        var page = pdf.Pages[0];
        Assert.That(page.Width, Is.EqualTo(expected.Width * PointsPerMm).Within(Tolerance));
        Assert.That(page.Height, Is.EqualTo(expected.Height * PointsPerMm).Within(Tolerance));
    }

    [Test]
    public async Task Default_Is_A4_Portrait_With_10mm_Margins()
    {
        var pdf = await Create(new HtmlInput { HtmlContent = Probe("Corner") });

        var page = pdf.Pages[0];
        Assert.That(page.Width, Is.EqualTo(210 * PointsPerMm).Within(Tolerance));
        Assert.That(page.Height, Is.EqualTo(297 * PointsPerMm).Within(Tolerance));
        AssertStartsAt(page.Word("Corner"), 10, 10);
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

        var pdf = await Create(input);

        AssertStartsAt(pdf.Pages[0].Word("Corner"), marginMm, marginMm);
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

        var pdf = await Create(input);

        Assert.That(pdf.PageCount, Is.EqualTo(3));
        foreach (var (page, number) in pdf.Pages.Select((page, i) => (page, i + 1)))
        {
            var header = page.Word("HeaderText");
            var footer = page.Word("FooterText");
            var body = page.Word($"Body{number}");

            // the header's band lies below the top margin, the body below the header's band
            AssertStartsAt(header, margin, margin);
            Assert.That(header.Box.Top, Is.LessThan((margin + headerHeight) * PointsPerMm));
            AssertStartsAt(body, margin + headerHeight, margin);
            // the footer's band ends at the bottom margin
            Assert.That(footer.Box.Top, Is.GreaterThanOrEqualTo(page.Height - (margin + footerHeight) * PointsPerMm - Tolerance));
            Assert.That(page.Height - footer.Box.Bottom, Is.GreaterThanOrEqualTo(margin * PointsPerMm - Tolerance));
            Assert.That(footer.Box.Left, Is.EqualTo(margin * PointsPerMm).Within(Tolerance + 1));
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

        var pdf = await Create(input);

        Assert.That(pdf.Pages[0].Words.Select(word => word.Text), Is.EquivalentTo(new[] { "HeaderText", "Body" }));
    }

    /// <summary>
    /// <c>lorem-ipsum.html</c> filled by <see cref="HtmlTemplateParser"/>: a title, a logo and a row per item, with
    /// <c>header.html</c> as the header.
    /// </summary>
    [Test]
    public async Task Template_Fills_Every_Parameter_And_Repeats_The_Header()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer());
        var parameters = new
        {
            title = "Quarterly report",
            date = "08/10/2026",
            lijn = Enumerable.Range(0, 12).Select(i => new { title = $"Item {(char)('A' + i)}" }).ToList(),
            logo = $"data:image/png;base64,{Convert.ToBase64String(await File.ReadAllBytesAsync(InputPath("regira-logo.png")))}"
        };
        var input = new HtmlInput
        {
            HtmlContent = await parser.Parse(await File.ReadAllTextAsync(InputPath("lorem-ipsum.html")), parameters),
            HeaderHtmlContent = await parser.Parse(await File.ReadAllTextAsync(InputPath("header.html")), parameters),
            HeaderHeight = 10
        };

        var pdf = await Create(input);

        var text = string.Join(" ", pdf.PageTexts);
        Assert.That(pdf.PageCount, Is.GreaterThan(1), "the rows run over several pages");
        Assert.That(text, Does.Contain(parameters.title));
        foreach (var row in parameters.lijn)
        {
            Assert.That(text, Does.Contain(row.title));
        }
        Assert.That(text, Does.Not.Match(@"\{\w+\}"), "a parameter left unfilled");
        Assert.That(pdf.Pages[0].Images, Is.Not.Empty, "the logo");
        foreach (var (page, number) in pdf.Pages.Select((page, i) => (page, i + 1)))
        {
            Assert.That(page.Text, Does.Contain("Regira").And.Contain(parameters.date), $"page {number}'s header");
        }
    }


    private async Task<PdfFacts> Create(HtmlInput input)
    {
        using var file = await Backend.Create(input);
        return await ReadPdf(file);
    }

    // a browser's default style puts 8px around the body
    private const string ResetBody = "<style>body{margin:0}</style>";
    // one word at the content box's top-left corner, its glyphs filling the line
    private static string Probe(string word) => $"{ResetBody}<div style=\"font-size:20px;line-height:1\">{word}</div>";

    // a glyph box starts a little right of and below its line box's corner
    private static void AssertStartsAt(WordFact word, double topMm, double leftMm)
    {
        Assert.That(word.Box.Left, Is.EqualTo(leftMm * PointsPerMm).Within(Tolerance + 1),
            $"'{word.Text}' left");
        Assert.That(word.Box.Top, Is.InRange(topMm * PointsPerMm - Tolerance, topMm * PointsPerMm + 6),
            $"'{word.Text}' top");
    }
}
