using System.IO.Compression;
using Docnet.Core;
using Docnet.Core.Models;
using DocumentFormat.OpenXml.Packaging;
using Office.Word.testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.PDF.DocNET;
using Regira.Office.Word.Aspose;
using Regira.Office.Word.Models;
using Regira.Utilities;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Office.Word.testing;

/// <summary>
/// Shared scenarios come from <see cref="WordTestsBase"/>; everything here is either a case list or
/// an Aspose-specific difference.
/// </summary>
/// <remarks>
/// Runs with or without a licence. Unlicensed, Aspose.Words watermarks every document and truncates long
/// ones; the shared scenarios check that content is <em>present</em>, so they pass against that evaluation
/// output too. What a licence changes is covered by the two licence tests, which run only when one is
/// configured (<c>ASPOSE_WORDS_LICENSE</c>, Base64, or <c>ASPOSE_WORDS_LICENSE_PATH</c>), and by
/// <see cref="AsposeEvaluationTests"/>, which runs only when none is and proves those two checks fail
/// against evaluation output.
/// </remarks>
[TestFixture]
[Category("License")]
public class AsposeTests() : WordTestsBase(new WordService(LicenseFromEnvironment()), "Aspose")
{
    /// <summary>
    /// Words the evaluation watermark carries. Deliberately broad: they match whatever wording Aspose
    /// uses, and none of the test assets contains either.
    /// </summary>
    internal static readonly string[] EvaluationMarkers = ["Aspose", "Evaluation"];

    internal static bool HasLicence
        => !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AsposeLicense.LicenseVariable))
           || !string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(AsposeLicense.LicensePathVariable));

    /// <summary>
    /// The licence handed over through <see cref="AsposeWordConfig"/>, the way an application configures
    /// it. Nothing else in the suite applies a licence, so a licensed run exercises this route; the
    /// environment fallback is covered by <see cref="AsposeLicenseTests"/>. Without one the shared scenarios
    /// run on evaluation output, which they tolerate, so this fixture opts into it.
    /// </summary>
    internal static AsposeWordConfig LicenseFromEnvironment() => new()
    {
        LicenseBase64 = Environment.GetEnvironmentVariable(AsposeLicense.LicenseVariable),
        LicensePath = Environment.GetEnvironmentVariable(AsposeLicense.LicensePathVariable),
        AllowEvaluation = true
    };

    private readonly PdfManager _pdf = new(new ImageService());


    private static void RequireLicence()
    {
        if (!HasLicence)
        {
            Assert.Ignore(
                $"Set {AsposeLicense.LicenseVariable} (the licence file, Base64-encoded) or {AsposeLicense.LicensePathVariable} " +
                "to verify the licence lifts the evaluation limits.");
        }
    }


    /// <summary>
    /// Answers the licensing question outright: a licence that does not cover Aspose.Words leaves the
    /// evaluation banner in, and every shared scenario still passes against it because the banner is only
    /// additional text.
    /// </summary>
    [Test]
    public async Task License_Removes_The_Evaluation_Watermark()
    {
        RequireLicence();

        var text = await Service.GetText(TemplateInput("lorem_ipsum.docx"));
        // a watermark can also be a shape, which only shows once the page is rendered
        using var pdf = await Service.Convert(TemplateInput("lorem_ipsum.docx"), FileFormat.Pdf);
        var pdfText = await _pdf.GetText(pdf);

        Assert.Multiple(() =>
        {
            foreach (var marker in EvaluationMarkers)
            {
                Assert.That(text, Does.Not.Contain(marker).IgnoreCase, "The licence does not cover Aspose.Words: the text carries the evaluation watermark.");
                Assert.That(pdfText, Does.Not.Contain(marker).IgnoreCase, "The licence does not cover Aspose.Words: the rendered pages carry the evaluation watermark.");
            }
        });
    }

    [Test]
    public async Task Long_Document_Is_Not_Truncated()
    {
        RequireLicence();

        // evaluation mode stops after a few hundred paragraphs, whatever its watermark says
        var text = await Service.GetText(new WordTemplateInput { Template = LongDocument() });

        Assert.That(text, Does.Contain(LongDocumentLastParagraph));
    }


    [TestCase("template.dot")]
    [TestCase("template.doc")]
    [TestCase("template.odt")]
    [TestCase("multipage.docx")]
    public override Task From_File(string filename) => base.From_File(filename);

    [Test]
    public override Task Bookmarks() => base.Bookmarks();

    [Test]
    public override Task Merge() => base.Merge();

    [Test]
    public override Task Add_Header_And_Footer() => base.Add_Header_And_Footer();

    [Test]
    public override Task Add_FirstPage_Header_And_Footer() => base.Add_FirstPage_Header_And_Footer();

    [Test]
    public override Task Replace_Parameters() => base.Replace_Parameters();

    [Test]
    public override Task Replace_Image() => base.Replace_Image();

    [Test]
    public override Task Template_Row() => base.Template_Row();

    [Test]
    public override Task Nested_Documents() => base.Nested_Documents();

    [Test]
    public override Task Nested_Documents_Do_Not_Wear_Out_The_Service() => base.Nested_Documents_Do_Not_Wear_Out_The_Service();

    [Test]
    public override void A_Template_That_Includes_Itself_Fails() => base.A_Template_That_Includes_Itself_Fails();

    [Test]
    public override Task A_Missing_Collection_Table_Leaves_The_Others() => base.A_Missing_Collection_Table_Leaves_The_Others();

    [Test]
    public override Task A_Null_Or_Unused_Parameter_Is_Harmless() => base.A_Null_Or_Unused_Parameter_Is_Harmless();

    [TestCase(FileFormat.Pdf, "converted.pdf")]
    [TestCase(FileFormat.Html, "converted.html")]
    [TestCase(FileFormat.Rtf, "converted.rtf")]
    [TestCase(FileFormat.Odt, "converted.odt")]
    [TestCase(FileFormat.EPub, "converted.epub")]
    [TestCase(FileFormat.Doc, "converted.doc")]
    [TestCase(FileFormat.Dotx, "converted.dotx")]
    public override Task Convert_To(FileFormat format, string outputName) => base.Convert_To(format, outputName);

    [Test]
    public override Task From_A3_To_Pdf() => base.From_A3_To_Pdf();

    [Test]
    public override Task From_A4_To_Pdf_A3() => base.From_A4_To_Pdf_A3();

    [Test]
    public override Task To_Images() => base.To_Images();

    [Test]
    public override Task GetImages() => base.GetImages();

    [Test]
    public override Task GetText() => base.GetText();


    [Test]
    public async Task Odt_Template_Is_Read()
    {
        var text = await Service.GetText(TemplateInput("template.odt"));

        Assert.That(text, Is.Not.Empty);
    }

    [TestCase(FileFormat.Png)]
    [TestCase(FileFormat.Jpeg)]
    public void Convert_To_Image_Points_At_ToImages(FileFormat format)
    {
        var ex = Assert.ThrowsAsync<NotSupportedException>(
            () => Service.Convert(TemplateInput("template.docx"), format));
        Assert.That(ex!.Message, Does.Contain("ToImages"));
    }

    [TestCase(FileFormat.Pdf, "application/pdf")]
    [TestCase(FileFormat.Html, "text/html")]
    [TestCase(FileFormat.Odt, "application/vnd.oasis.opendocument.text")]
    [TestCase(FileFormat.EPub, "application/epub+zip")]
    public async Task Convert_Tags_The_Actual_Output_Format(FileFormat format, string contentType)
    {
        // Word.Spire reports a Word content type for every format; this backend does not.
        using var output = await Service.Convert(TemplateInput("template.docx"), format);

        Assert.That(output.ContentType, Is.EqualTo(contentType));
    }

    [Test]
    public async Task Convert_To_Html_Is_Self_Contained()
    {
        using var output = await Service.Convert(TemplateInput("template.docx"), FileFormat.Html);
        var html = System.Text.Encoding.UTF8.GetString(output.GetBytes()!);

        // Saving HTML to a stream has nowhere to put an images folder or a stylesheet.
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("<img"));
            Assert.That(html, Does.Contain("src=\"data:image"));
            Assert.That(html, Does.Not.Contain(".png\""));
            Assert.That(html, Does.Not.Contain(".css\""));
        });
    }

    [Test]
    public async Task Convert_To_EPub_Writes_An_Epub_Package()
    {
        using var output = await Service.Convert(TemplateInput("template.docx"), FileFormat.EPub);

        using var zip = new ZipArchive(new MemoryStream(output.GetBytes()!), ZipArchiveMode.Read);
        using var reader = new StreamReader(zip.GetEntry("mimetype")!.Open());
        Assert.That(reader.ReadToEnd().Trim(), Is.EqualTo("application/epub+zip"));
    }

    [Test]
    public async Task ToImages_Returns_One_Image_Per_Page()
    {
        using var pdf = await Service.Convert(TemplateInput("multipage.docx"), FileFormat.Pdf);
        var pageCount = await _pdf.GetPageCount(pdf);

        var images = (await Service.ToImages(TemplateInput("multipage.docx"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(pageCount, Is.GreaterThan(1));
            Assert.That(images, Has.Count.EqualTo(pageCount));
        });
        images.ForEach(image => image.Dispose());
    }

    [TestCase(PageSize.A3, PageOrientation.Portrait, 842, 1191)]
    [TestCase(PageSize.A4, PageOrientation.Landscape, 842, 595)]
    // below A5: a size Aspose's own paper-size list does not name
    [TestCase(PageSize.A6, PageOrientation.Portrait, 298, 420)]
    public async Task Page_Settings_Set_The_Rendered_Page_Size(PageSize size, PageOrientation orientation, int width, int height)
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = size, PageOrientation = orientation }
        };

        using var pdf = await Service.Convert(TemplateInput("template.docx"), options);
        var page = FirstPageSize(pdf.GetBytes()!);

        Assert.Multiple(() =>
        {
            Assert.That(page.Width, Is.EqualTo(width).Within(2));
            Assert.That(page.Height, Is.EqualTo(height).Within(2));
        });
    }

    [Test]
    public async Task Multiline_Parameter_Becomes_A_Line_Break()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "First line\r\nSecond line" };

        using var output = await Service.Create(input);

        using var doc = WordprocessingDocument.Open(new MemoryStream(output.GetBytes()!), false);
        var paragraph = doc.MainDocumentPart!.Document!.Body!.Descendants<W.Paragraph>()
            .Single(p => p.InnerText.Contains("First line"));
        Assert.Multiple(() =>
        {
            Assert.That(paragraph.InnerText, Does.Contain("Second line"));
            // a soft line break within the paragraph, not a literal control character
            Assert.That(paragraph.Descendants<W.Break>().Any(b => b.Type == null || b.Type.Value == W.BreakValues.TextWrapping), Is.True);
            Assert.That(paragraph.InnerText, Does.Not.Contain("\v"));
        });
    }

    [Test]
    public async Task Parameter_Value_Is_Inserted_Verbatim()
    {
        // "&p", "&l" and "$1" mean something to some replace APIs; a parameter value must not
        const string value = "Tom & Jerry &p &l $1";
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = value };

        var text = await Service.GetText(new WordTemplateInput { Template = await Service.Create(input) });

        Assert.That(text, Does.Contain(value));
    }

    [Test]
    public async Task Html_Parameter_Is_Injected()
    {
        var input = new WordTemplateInput
        {
            Template = DocxWithParagraphs("Notes: {{ html_notes }}", "After the notes"),
            GlobalParameters = new Dictionary<string, object> { ["html_notes"] = "<p>This is <strong>bold</strong> text.</p>" }
        };

        using var output = await Service.Create(input);
        await output.SaveAs(OutputPath("html_parameter.docx"));
        var text = await Service.GetText(new WordTemplateInput { Template = output });

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("This is bold text."));
            Assert.That(text, Does.Contain("After the notes"));
            Assert.That(text, Does.Not.Contain("<strong>"));
            Assert.That(text, Does.Not.Contain("html_notes"));
        });
    }

    [Test]
    public async Task DocumentBuilder_Builds_Paragraphs_And_Headers()
    {
        var image = ReadAsset("sample1.jpg");
        var paragraphs = new List<Paragraph>
        {
            new() { Text = "Lorem Ipsum", Style = ParagraphStyle.Heading1 },
            new() { Text = LoremIpsum.Paragraphs.First(), PageBreakAfter = true },
            new() { Text = "Second page", PageBreakAfter = true, Image = new WordImage { Name = "sample", File = image, Size = new(300, 169), HorizontalAlignment = HorizontalAlignment.Right } },
            new() { Text = "Third page" }
        };

        var builder = new Regira.Office.Word.Aspose.DocumentBuilder((WordService)Backend);
        using var docx = await builder.WithParagraphs(paragraphs)
            .AddHeader(new WordHeaderFooterInput { Template = TemplateInput("firstpage_header.docx"), Type = HeaderFooterType.FirstPage })
            .AddHeader(new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") })
            .Build();
        await docx.SaveAs(OutputPath("document_builder.docx"));

        using var pdf = await Service.Convert(new WordTemplateInput { Template = docx }, FileFormat.Pdf);
        var pageCount = await _pdf.GetPageCount(pdf);
        var text = await Service.GetText(new WordTemplateInput { Template = docx });
        var images = await Service.GetImages(new WordTemplateInput { Template = docx });

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Lorem Ipsum"));
            Assert.That(text, Does.Contain("Header"));
            // the two page breaks after a paragraph
            Assert.That(pageCount, Is.EqualTo(3));
            // the header templates bring their own logo along
            Assert.That(images.Select(i => i.Name), Does.Contain("sample"));
        });
    }


    private static (int Width, int Height) FirstPageSize(byte[] pdf)
    {
        using var reader = DocLib.Instance.GetDocReader(pdf, new PageDimensions(1d));
        using var page = reader.GetPageReader(0);
        return (page.GetPageWidth(), page.GetPageHeight());
    }

    private const int LongDocumentParagraphCount = 1000;
    internal static readonly string LongDocumentLastParagraph = $"Paragraph {LongDocumentParagraphCount} of {LongDocumentParagraphCount}";

    /// <summary>
    /// Well past the evaluation mode's paragraph limit.
    /// </summary>
    internal static IMemoryFile LongDocument()
        => DocxWithParagraphs(Enumerable.Range(1, LongDocumentParagraphCount)
            .Select(i => $"Paragraph {i} of {LongDocumentParagraphCount}")
            .ToArray());

    internal static IMemoryFile DocxWithParagraphs(params string[] paragraphs)
    {
        var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            main.Document = new W.Document(new W.Body(paragraphs.Select(text => new W.Paragraph(new W.Run(new W.Text(text))))));
        }
        return stream.ToArray().ToBinaryFile();
    }
}
