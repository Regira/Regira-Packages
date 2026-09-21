using Docnet.Core;
using Docnet.Core.Models;
using Microsoft.Extensions.Configuration;
using Office.Word.testing.Abstractions;
using Regira.Drawing.SkiaSharp.Services;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.PDF.DocNET;
using Regira.Office.Word.Models;
using Regira.Office.Word.Syncfusion;

namespace Office.Word.testing;

/// <summary>
/// Shared scenarios come from <see cref="WordTestsBase"/>; everything here is either a case list
/// or a DocIO-specific difference.
/// </summary>
[TestFixture]
[Category("License")]
public class SyncfusionTests() : WordTestsBase(CreateService(), "Syncfusion")
{
    private const string LicenseVariable = "SYNCFUSION_LICENSE_KEY";
    private const string LicenseSetting = "SyncFusion:LicenseKey";

    /// <summary>
    /// The key comes from user secrets (<c>dotnet user-secrets set "SyncFusion:LicenseKey" "..."</c>),
    /// falling back to the <c>SYNCFUSION_LICENSE_KEY</c> environment variable the package itself reads,
    /// so CI can hand it over without a secrets file. Null when neither is set.
    /// </summary>
    private static readonly string? LicenseKey =
        new ConfigurationBuilder()
            .AddUserSecrets(typeof(SyncfusionTests).Assembly, optional: true)
            .Build()[LicenseSetting]
        ?? Environment.GetEnvironmentVariable(LicenseVariable);

    /// <summary>
    /// The banner DocIO prepends to every document when the key is missing, wrong, or does not
    /// cover the Word library.
    /// </summary>
    private const string TrialBanner = "trial version of Syncfusion Word library";

    private static WordService CreateService()
        => new(new SyncfusionWordConfig { LicenseKey = LicenseKey });


    /// <summary>
    /// Answers the licensing question outright: a key that does not license DocIO produces a
    /// watermarked document, and every other assertion in this fixture still passes against one
    /// because the banner is only additional text.
    /// </summary>
    [Test]
    public async Task License_Key_Removes_The_Trial_Banner()
    {
        if (string.IsNullOrWhiteSpace(LicenseKey))
        {
            // Only this test needs a key. The rest run unlicensed: the banner is extra text, so it
            // does not affect what they assert.
            Assert.Ignore($"Set the {LicenseSetting} user secret or {LicenseVariable} to verify the key licenses DocIO.");
        }

        var text = await Service.GetText(TemplateInput("lorem_ipsum.docx"));

        Assert.That(text, Does.Not.Contain(TrialBanner),
            $"The configured key ({LicenseSetting} / {LicenseVariable}) does not license the DocIO Word library — output is watermarked.");
    }


    // template.odt is absent: DocIO cannot load ODT (see Odt_Template_Is_Not_Supported).
    [TestCase("template.dot")]
    [TestCase("template.doc")]
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

    [Test]
    public override Task A_Parameter_Key_Is_Matched_Literally() => base.A_Parameter_Key_Is_Matched_Literally();

    [TestCase(HeaderFooterType.Even, true)]
    [TestCase(HeaderFooterType.Even, false)]
    [TestCase(HeaderFooterType.FirstPage, true)]
    [TestCase(HeaderFooterType.FirstPage, false)]
    public override Task A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(HeaderFooterType type, bool specialHeader)
        => base.A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(type, specialHeader);

    // EPub is absent: unavailable on .NET Core (see Convert_To_EPub_Is_Not_Supported).
    [TestCase(FileFormat.Pdf, "converted.pdf")]
    [TestCase(FileFormat.Html, "converted.html")]
    [TestCase(FileFormat.Rtf, "converted.rtf")]
    [TestCase(FileFormat.Odt, "converted.odt")]
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
    public void Odt_Template_Is_Not_Supported()
    {
        // DocIO can save ODT but cannot load it.
        var ex = Assert.ThrowsAsync<NotSupportedException>(() => Service.Create(TemplateInput("template.odt")));
        Assert.That(ex!.Message, Does.Contain("ODT"));
    }

    [Test]
    public void Convert_To_EPub_Is_Not_Supported()
    {
        var ex = Assert.ThrowsAsync<NotSupportedException>(
            () => Service.Convert(TemplateInput("template.docx"), FileFormat.EPub));
        Assert.That(ex!.Message, Does.Contain("EPUB"));
    }

    [TestCase(FileFormat.Png)]
    [TestCase(FileFormat.Jpeg)]
    public void Convert_To_Image_Points_At_ToImages(FileFormat format)
    {
        var ex = Assert.ThrowsAsync<NotSupportedException>(
            () => Service.Convert(TemplateInput("template.docx"), format));
        Assert.That(ex!.Message, Does.Contain("ToImages"));
    }

    [Test]
    public async Task Convert_Tags_The_Actual_Output_Format()
    {
        // Word.Spire reports a Word content type for every format; this backend does not.
        using var pdf = await Service.Convert(TemplateInput("template.docx"), FileFormat.Pdf);
        using var html = await Service.Convert(TemplateInput("template.docx"), FileFormat.Html);

        Assert.Multiple(() =>
        {
            Assert.That(pdf.ContentType, Is.EqualTo("application/pdf"));
            Assert.That(html.ContentType, Does.Contain("html"));
        });
    }

    [TestCase(PageSize.A3, PageOrientation.Portrait, 842, 1191)]
    [TestCase(PageSize.A4, PageOrientation.Landscape, 842, 595)]
    // sizes DocIO's own PageSize list does not name
    [TestCase(PageSize.A2, PageOrientation.Portrait, 1191, 1684)]
    [TestCase(PageSize.A7, PageOrientation.Portrait, 210, 298)]
    public async Task Page_Settings_Set_The_Rendered_Page_Size(PageSize size, PageOrientation orientation, int width, int height)
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = size, PageOrientation = orientation }
        };

        using var pdf = await Service.Convert(TemplateInput("template.docx"), options);
        using var reader = DocLib.Instance.GetDocReader(pdf.GetBytes()!, new PageDimensions(1d));
        using var page = reader.GetPageReader(0);

        Assert.Multiple(() =>
        {
            Assert.That(page.GetPageWidth(), Is.EqualTo(width).Within(2));
            Assert.That(page.GetPageHeight(), Is.EqualTo(height).Within(2));
        });
    }

    [Test]
    public async Task DocumentBuilder_Builds_Paragraphs_And_Headers()
    {
        var image = ReadAsset("sample1.jpg");
        var paragraphs = new List<Paragraph>
        {
            new() { Text = "Lorem Ipsum", Style = ParagraphStyle.Heading1 },
            new() { Text = LoremIpsum.Paragraphs.First(), PageBreakAfter = true, HorizontalAlignment = HorizontalAlignment.Justify },
            // a picture cannot be justified; it keeps its default position
            new() { Text = "Second page", PageBreakAfter = true, Image = new WordImage { Name = "sample", File = image, Size = new(300, 169), HorizontalAlignment = HorizontalAlignment.Justify } },
            new() { Text = "Third page" }
        };

        var builder = new DocumentBuilder((WordService)Backend);
        using var docx = await builder.WithParagraphs(paragraphs)
            .AddHeader(new WordHeaderFooterInput { Template = TemplateInput("firstpage_header.docx"), Type = HeaderFooterType.FirstPage })
            .AddHeader(new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") })
            .Build();
        await docx.SaveAs(OutputPath("document_builder.docx"));

        using var pdf = await Service.Convert(new WordTemplateInput { Template = docx }, FileFormat.Pdf);
        var pageCount = await new PdfManager(new ImageService()).GetPageCount(pdf);
        var text = await Service.GetText(new WordTemplateInput { Template = docx });
        var images = await Service.GetImages(new WordTemplateInput { Template = docx });

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Lorem Ipsum"));
            Assert.That(text, Does.Contain("Header"));
            // the two page breaks after a paragraph
            Assert.That(pageCount, Is.EqualTo(3));
            Assert.That(images.Select(i => i.Name), Does.Contain("sample"));
        });
    }

    [Test]
    public async Task Convert_To_Html_Is_Self_Contained()
    {
        using var output = await Service.Convert(TemplateInput("template.docx"), FileFormat.Html);
        var html = System.Text.Encoding.UTF8.GetString(output.GetBytes()!);

        // Saving HTML to a stream has nowhere to put an images folder, so DocIO inlines the
        // images as data URIs. Nothing may point at a file next to the document.
        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("<img"));
            Assert.That(html, Does.Contain("src=\"data:image"));
            Assert.That(html, Does.Not.Contain(".png\""));
            Assert.That(html, Does.Not.Contain(".css\""));
        });
    }
}
