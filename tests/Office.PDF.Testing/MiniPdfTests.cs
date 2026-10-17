using Office.PDF.Testing.Abstractions;
using Regira.Office.Models;
using Regira.Office.PDF.MiniPdf;
using Regira.Office.PDF.Models;

namespace Office.PDF.Testing;

/// <summary>
/// PDF.MiniPdf, the only <c>IDocumentToPdfService</c>: Word, Excel and PowerPoint to PDF, read back with
/// <see cref="PdfFacts"/>.
/// </summary>
[TestFixture]
public class MiniPdfTests() : PdfAssetsTestsBase("MiniPdf")
{
    // A5 is 148 × 210 mm
    private const double A5Short = 419.53;
    private const double A5Long = 595.28;

    private readonly PdfService _pdfService = new();

    [TestCase("lorem-ipsum.docx", "Lorem ipsum")]
    [TestCase("lorem-ipsum.xlsx", "Ipsum")]
    [TestCase("lorem-ipsum.pptx", "Dolor sit amet")]
    public async Task Converts_To_Pdf(string fileName, string expectedText)
    {
        using var pdf = await _pdfService.Create(new DocumentInput { Document = ReadAsset(fileName) });

        var facts = await ReadPdf(pdf);
        Assert.That(pdf.ContentType, Is.EqualTo("application/pdf"));
        Assert.That(string.Join(" ", facts.PageTexts), Does.Contain(expectedText));
    }

    // lorem-ipsum.xlsx binds SpreadsheetML to a prefix (<x:workbook>), as the Open XML SDK, ClosedXML and MiniExcel
    // write it; the default-namespace one declares it as the default, as Excel does; the mixed one is that workbook
    // with its first sheet edited through the Open XML SDK, which writes only that part with the prefix
    [TestCase("lorem-ipsum.xlsx")]
    [TestCase("lorem-ipsum-default-namespace.xlsx")]
    [TestCase("lorem-ipsum-mixed-namespace.xlsx")]
    public async Task Xlsx_Renders_Every_Sheet(string fileName)
    {
        using var pdf = await _pdfService.Create(new DocumentInput { Document = ReadAsset(fileName) });

        var text = string.Join(" ", (await ReadPdf(pdf)).PageTexts);
        Assert.That(text, Does.Contain("Lorem"));
        Assert.That(text, Does.Contain("Sit amet consectetur"));
    }

    [Test]
    public async Task Docx_Takes_Format_And_Orientation()
    {
        var input = new DocumentInput
        {
            Document = ReadAsset("lorem-ipsum.docx"),
            Format = PageSize.A5,
            Orientation = PageOrientation.Landscape
        };

        using var pdf = await _pdfService.Create(input);

        var facts = await ReadPdf(pdf);
        Assert.That(facts.Pages.Select(page => page.Width), Is.All.EqualTo(A5Long).Within(1));
        Assert.That(facts.Pages.Select(page => page.Height), Is.All.EqualTo(A5Short).Within(1));
    }

    [Test]
    public async Task Docx_Takes_Margins()
    {
        using var defaultPdf = await _pdfService.Create(new DocumentInput { Document = ReadAsset("lorem-ipsum.docx") });
        using var widePdf = await _pdfService.Create(new DocumentInput { Document = ReadAsset("lorem-ipsum.docx"), Margins = 150f });

        var defaultFacts = await ReadPdf(defaultPdf, "-default");
        var wideFacts = await ReadPdf(widePdf, "-wide");
        Assert.That(wideFacts.PageCount, Is.GreaterThan(defaultFacts.PageCount));
        // the body starts 150 pt from the page's left and top edges; the document's header sits above it
        var body = wideFacts.Pages[0].Word("Lorem").Box;
        Assert.That(body.Left, Is.EqualTo(150).Within(1));
        Assert.That(body.Top, Is.GreaterThanOrEqualTo(150));
    }

    [Test]
    public async Task Xlsx_Takes_Orientation()
    {
        var input = new DocumentInput { Document = ReadAsset("lorem-ipsum.xlsx"), Orientation = PageOrientation.Landscape };

        using var pdf = await _pdfService.Create(input);

        var facts = await ReadPdf(pdf);
        Assert.That(facts.Pages.Select(page => page.Width > page.Height), Is.All.True);
    }

    [TestCase("lorem-ipsum.docx", null, PageOrientation.Landscape, false)]
    [TestCase("lorem-ipsum.xlsx", PageSize.A5, null, false)]
    [TestCase("lorem-ipsum.xlsx", null, null, true)]
    [TestCase("lorem-ipsum.pptx", PageSize.A5, null, false)]
    [TestCase("lorem-ipsum.pptx", null, PageOrientation.Landscape, false)]
    [TestCase("lorem-ipsum.pptx", null, null, true)]
    public async Task Refuses_A_Setting_The_Format_Cannot_Take(string fileName, PageSize? format, PageOrientation? orientation, bool margins)
    {
        var input = new DocumentInput
        {
            Document = ReadAsset(fileName),
            Format = format,
            Orientation = orientation,
            Margins = margins ? (Margins)20f : null
        };

        await Assert.ThrowsAsync<NotSupportedException>(() => _pdfService.Create(input));
    }

    [TestCase("sample.pdf")]
    [TestCase("lorem-ipsum.html")]
    public async Task Refuses_A_Document_That_Is_Not_Office_Open_Xml(string fileName)
    {
        await Assert.ThrowsAsync<NotSupportedException>(() => _pdfService.Create(new DocumentInput { Document = ReadAsset(fileName) }));
    }
}
