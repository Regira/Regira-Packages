using NUnit.Framework.Legacy;
using Regira.IO.Extensions;
using Regira.IO.Utilities;
using Regira.Office.PDF.Models;
using Regira.Office.PDF.Puppeteer;
using Regira.Serializing.Newtonsoft.Json;
using Regira.Utilities;
using Regira.Web.HTML;

namespace Office.PDF.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class PuppeteerTests
{
    private readonly string _tableContent;
    private readonly string _loremIpsumContent;
    private readonly string _regiraLogoPath;
    private readonly HtmlTemplateParser _htmlParser;
    private readonly string _inputDir;
    private readonly string _outputDir;

    public PuppeteerTests()
    {
        _htmlParser = new HtmlTemplateParser(new JsonSerializer());

        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        var assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        _inputDir = Path.Combine(assetsDir, "Input");
        _outputDir = Path.Combine(assetsDir, "Output", "Puppeteer");
        Directory.CreateDirectory(_outputDir);
        _tableContent = File.ReadAllText(Path.Combine(assetsDir, "Input", "table.html"));
        _loremIpsumContent = File.ReadAllText(Path.Combine(assetsDir, "Input", "lorem-ipsum.html"));
        _regiraLogoPath = Path.Combine(assetsDir, "Input", "regira-logo.png");
    }

    [Test]
    public async Task CreatePdf()
    {
        var html = _htmlParser.Parse(_tableContent, (object?)null).Result;

        var pdfPrinter = new PdfManager();
        var template = new HtmlInput
        {
            HtmlContent = html
        };
        using var pdf = await pdfPrinter.Create(template);
        using var stream = pdf.GetStream()!;
        ClassicAssert.IsNotNull(stream);
        Assert.That(stream.Length > 0, Is.True);
        var outputPath = Path.Combine(_outputDir, "pdf-table.pdf");
        File.WriteAllBytes(outputPath, FileUtility.GetBytes(stream)!);
    }

    [Test]
    public async Task FillParameters()
    {
        var date = DateTime.Now;
        var logoBytes = File.ReadAllBytes(_regiraLogoPath);
        var logoBase64 = $"data:image/png;base64,{FileUtility.GetBase64String(logoBytes)}";
        var parameters = new
        {
            title = "Test Title",
            date = date.ToString("dd/MM/yyyy"),
            lijn = Enumerable.Repeat((object?)null, 25).Select((_, i) => new { title = $"Item {char.ConvertFromUtf32(i + 65)}" }).ToList(),
            logo = logoBase64
        };

        var html = _htmlParser.Parse(_loremIpsumContent, parameters).Result;

        var template = new HtmlInput
        {
            HtmlContent = html
        };

        var pdfPrinter = new PdfManager();
        using var pdf = await pdfPrinter.Create(template);
        using var stream = pdf.GetStream()!;
        ClassicAssert.IsNotNull(stream);
        Assert.That(stream.Length > 0, Is.True);
        var outputPath = Path.Combine(_outputDir, "pdf-with-parameters.pdf");
        File.WriteAllBytes(outputPath, FileUtility.GetBytes(stream)!);
    }
}
