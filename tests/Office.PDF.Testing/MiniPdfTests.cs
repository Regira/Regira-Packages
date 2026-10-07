using Docnet.Core;
using Docnet.Core.Models;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.PDF.Models;
using Regira.Office.PDF.MiniPdf;
using Regira.Utilities;
using DocNetPdfManager = Regira.Office.PDF.DocNET.PdfManager;

namespace Office.PDF.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class MiniPdfTests
{
    private readonly PdfService _pdfService = new();
    // reads the output back independently of the backend that wrote it
    private readonly DocNetPdfManager _reader = new(null!);
    private readonly string _inputDir;
    private readonly string _outputDir;

    public MiniPdfTests()
    {
        var assetsDir = Path.Combine(AssemblyUtility.GetAssemblyDirectory()!, "../../../", "Assets");
        _inputDir = Path.Combine(assetsDir, "Input");
        _outputDir = Path.Combine(assetsDir, "Output/MiniPdf");
        Directory.CreateDirectory(_outputDir);
    }

    [TestCase("lorem-ipsum.docx", "Lorem ipsum")]
    [TestCase("lorem-ipsum.xlsx", "Ipsum")]
    [TestCase("lorem-ipsum.pptx", "Dolor sit amet")]
    public async Task Converts_To_Pdf(string fileName, string expectedText)
    {
        using var pdf = await Convert(new DocumentInput { Document = Read(fileName) }, $"{fileName}.pdf");

        Assert.That(pdf.ContentType, Is.EqualTo("application/pdf"));
        Assert.That(pdf.GetBytes()![..5], Is.EqualTo("%PDF-"u8.ToArray()));
        Assert.That(await _reader.GetText(pdf), Does.Contain(expectedText));
    }

    // lorem-ipsum.xlsx binds SpreadsheetML to a prefix (<x:workbook>), as the Open XML SDK, ClosedXML and MiniExcel
    // write it; the default-namespace one declares it as the default, as Excel does; the mixed one is that workbook
    // with its first sheet edited through the Open XML SDK, which writes only that part with the prefix
    [TestCase("lorem-ipsum.xlsx")]
    [TestCase("lorem-ipsum-default-namespace.xlsx")]
    [TestCase("lorem-ipsum-mixed-namespace.xlsx")]
    public async Task Xlsx_Renders_Every_Sheet(string fileName)
    {
        using var pdf = await Convert(new DocumentInput { Document = Read(fileName) }, $"every-sheet-{fileName}.pdf");

        var text = await _reader.GetText(pdf);
        Assert.That(text, Does.Contain("Lorem"));
        Assert.That(text, Does.Contain("Sit amet consectetur"));
    }

    [Test]
    public async Task Docx_Takes_Format_And_Orientation()
    {
        var input = new DocumentInput
        {
            Document = Read("lorem-ipsum.docx"),
            Format = PageSize.A5,
            Orientation = PageOrientation.Landscape
        };

        using var pdf = await Convert(input, "a5-landscape.pdf");

        // A5 is 148 x 210 mm: 419.53 x 595.28 pt, which Docnet truncates
        Assert.That(PageSizes(pdf), Is.All.EqualTo((595, 419)));
    }

    [Test]
    public async Task Docx_Takes_Margins()
    {
        using var defaultPdf = await Convert(new DocumentInput { Document = Read("lorem-ipsum.docx") }, "default-margins.pdf");
        using var widePdf = await Convert(new DocumentInput { Document = Read("lorem-ipsum.docx"), Margins = 150f }, "wide-margins.pdf");

        Assert.That(await _reader.GetPageCount(widePdf), Is.GreaterThan(await _reader.GetPageCount(defaultPdf)));
    }

    [Test]
    public async Task Xlsx_Takes_Orientation()
    {
        var input = new DocumentInput { Document = Read("lorem-ipsum.xlsx"), Orientation = PageOrientation.Landscape };

        using var pdf = await Convert(input, "xlsx-landscape.pdf");

        Assert.That(PageSizes(pdf).Select(size => size.Width > size.Height), Is.All.True);
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
            Document = Read(fileName),
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
        await Assert.ThrowsAsync<NotSupportedException>(() => _pdfService.Create(new DocumentInput { Document = Read(fileName) }));
    }


    private IMemoryFile Read(string fileName)
        => File.ReadAllBytes(Path.Combine(_inputDir, fileName)).ToMemoryFile();

    private async Task<IMemoryFile> Convert(DocumentInput input, string outputFileName)
    {
        var pdf = await _pdfService.Create(input);
        await File.WriteAllBytesAsync(Path.Combine(_outputDir, outputFileName), pdf.GetBytes()!);
        return pdf;
    }

    /// <summary>
    /// The size of every page in whole points.
    /// </summary>
    private static IList<(int Width, int Height)> PageSizes(IMemoryFile pdf)
    {
        using var reader = DocLib.Instance.GetDocReader(pdf.GetBytes()!, new PageDimensions(1d));
        return Enumerable.Range(0, reader.GetPageCount())
            .Select(i =>
            {
                using var page = reader.GetPageReader(i);
                return (page.GetPageWidth(), page.GetPageHeight());
            })
            .ToList();
    }
}
