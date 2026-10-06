using Office.Word.testing.Abstractions;
using Regira.IO.Abstractions;
using Regira.Office.Models;
using Regira.Office.Word.Models;
using Regira.Office.Word.Spire;

namespace Office.Word.testing;

/// <summary>
/// Word.Spire runs every shared scenario in <see cref="WordTestsBase"/>; below are its case lists and what it does
/// differently.
/// </summary>
[WordFixture]
public class SpireTests() : WordTestsBase(new WordService(), "Spire")
{
    public static IEnumerable<string> SourceFiles => ["template.dot", "template.doc", "template.odt", "multipage.docx"];

    public static IEnumerable<TestCaseData> OutputFormats =>
    [
        Output(FileFormat.Pdf, "application/pdf"),
        Output(FileFormat.Html, "text/html"),
        Output(FileFormat.Rtf, "text/rtf"),
        Output(FileFormat.Odt, "application/vnd.oasis.opendocument.text"),
        Output(FileFormat.EPub, "application/epub+zip"),
        Output(FileFormat.Doc, "application/msword")
    ];

    protected override Task<IMemoryFile> Build(IEnumerable<Paragraph> paragraphs, IEnumerable<WordHeaderFooterInput> headers, ConversionOptions? conversion = null,
        WordTemplateInput[]? inputs = null, MergeOptions? merge = null, DocumentSettings? settings = null)
    {
        var builder = new DocumentBuilder((WordService)Backend).WithParagraphs(paragraphs);
        if (inputs != null)
        {
            builder.Load(inputs);
        }
        if (merge != null)
        {
            builder.WithMerge(merge);
        }
        if (settings != null)
        {
            builder.WithSettings(new WordDocumentSettings { PageSize = settings.PageSize, PageOrientation = settings.PageOrientation });
        }
        foreach (var header in headers)
        {
            builder.AddHeader(header);
        }
        return (conversion == null ? builder : builder.WithConversion(conversion)).Build();
    }

    /// <summary>
    /// FreeSpire.Doc's free edition writes the first three pages of a longer document to PDF, followed by a notice
    /// page in place of the rest. Rendering pages is not capped. Pins the vendor limit the guides state: an upgrade
    /// that moves it fails here.
    /// </summary>
    [Test]
    public override async Task ToImages_Returns_One_Image_Per_Page()
    {
        using var pdf = await Service.Convert(TemplateInput("multipage.docx"), FileFormat.Pdf);
        var pages = await ReadPdf(pdf);
        var images = (await Service.ToImages(TemplateInput("multipage.docx"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(pages.Pages, Is.EqualTo(4));
            Assert.That(pages.PageTexts[2], Does.Contain("Page 3"));
            Assert.That(pages.PageTexts[3], Does.Contain("you can only get the first 3 page"));
            Assert.That(images, Has.Count.EqualTo(5));
        });
        images.ForEach(image => image.Dispose());
    }
}
