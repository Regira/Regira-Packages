using Office.Word.testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
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

    /// <summary>
    /// The banner DocIO prepends to every document when the key is missing, wrong, or does not
    /// cover the Word library.
    /// </summary>
    private const string TrialBanner = "trial version of Syncfusion Word library";

    private static WordService CreateService()
        => new(new SyncfusionWordConfig { LicenseKey = Environment.GetEnvironmentVariable(LicenseVariable) });


    /// <summary>
    /// Answers the licensing question outright: a key that does not license DocIO produces a
    /// watermarked document, and every other assertion in this fixture still passes against one
    /// because the banner is only additional text.
    /// </summary>
    [Test]
    public async Task License_Key_Removes_The_Trial_Banner()
    {
        if (string.IsNullOrWhiteSpace(Environment.GetEnvironmentVariable(LicenseVariable)))
        {
            // Only this test needs a key. The rest run unlicensed: the banner is extra text, so it
            // does not affect what they assert.
            Assert.Ignore($"Set {LicenseVariable} to verify the key licenses DocIO.");
        }

        var text = await Service.GetText(TemplateInput("lorem_ipsum.docx"));

        Assert.That(text, Does.Not.Contain(TrialBanner),
            $"The key in {LicenseVariable} does not license the DocIO Word library — output is watermarked.");
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
