using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using NUnit.Framework.Legacy;
using Office.Word.testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Mini;
using Regira.Office.Word.Models;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Office.Word.testing;

/// <summary>
/// Shared scenarios come from <see cref="WordTestsBase"/>. MiniWord implements only
/// <c>IWordCreator</c>, <c>IWordTextExtractor</c> and <c>IWordImageExtractor</c>, so the fixture
/// declares the handful of scenarios that reach those and covers the rest of its behaviour below.
/// </summary>
[TestFixture]
public class MiniTests() : WordTestsBase(new WordService(), "Mini")
{
    private WordService Mini => (WordService)Backend;


    // MiniWord reads .docx only, so the .dot/.doc/.odt cases the other backends run are absent.
    [TestCase("multipage.docx")]
    public override Task From_File(string filename) => base.From_File(filename);

    [Test]
    public override Task Replace_Parameters() => base.Replace_Parameters();

    [Test]
    public override Task GetImages() => base.GetImages();

    [Test]
    public override Task GetText() => base.GetText();


    [Test]
    public async Task Create_Sets_Docx_ContentType()
    {
        using var output = await Mini.Create(TemplateInput("parameters.docx"));

        Assert.That(output.ContentType, Is.EqualTo(ContentTypes.DOCX));
    }

    [Test]
    public async Task Create_Returns_A_Rewound_Stream()
    {
        using var output = await Mini.Create(TemplateInput("parameters.docx"));

        // Consumers that read Stream directly (rather than through GetStream()) must see content.
        Assert.That(output.Stream, Is.Not.Null);
        Assert.That(output.Stream!.Position, Is.Zero);
    }

    [Test]
    public async Task GetText_Returns_Substituted_Content()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["date"] = "2026-09-15" };

        var text = await Mini.GetText(input);

        Assert.That(text, Does.Contain("2026-09-15"));
        Assert.That(text, Does.Contain("Parameters"));
        Assert.That(text, Does.Not.Contain("{{date}}"));
    }

    [Test]
    public async Task GetText_Separates_Paragraphs()
    {
        var text = await Mini.GetText(TemplateInput("parameters.docx"));

        // Paragraphs must not run together: "HeadingParameters" would mean no separator was written.
        Assert.That(text, Does.Contain("\n"));
        Assert.That(text, Does.Not.Contain("HeadingParameters"));
    }


    [Test]
    public async Task GetImages_Returns_Empty_For_A_Document_Without_Images()
    {
        var images = await Mini.GetImages(TemplateInput("parameters.docx"));

        Assert.That(images, Is.Empty);
    }


    [Test]
    public void Create_Rejects_DocumentParameters()
    {
        var input = TemplateInput("parameters.docx");
        input.DocumentParameters = new Dictionary<string, WordTemplateInput>
        {
            ["nested"] = TemplateInput("doc-1.docx")
        };

        var ex = Assert.ThrowsAsync<NotSupportedException>(() => Mini.Create(input));
        Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.DocumentParameters)));
    }

    [Test]
    public void Create_Rejects_Headers_And_Footers()
    {
        var input = TemplateInput("parameters.docx");
        input.Headers = [new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") }];
        input.Footers = [new WordHeaderFooterInput { Template = TemplateInput("add_footer.docx") }];

        var ex = Assert.ThrowsAsync<NotSupportedException>(() => Mini.Create(input));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.Headers)));
            Assert.That(ex.Message, Does.Contain(nameof(WordTemplateInput.Footers)));
        });
    }

    [Test]
    public void Create_Rejects_Unsupported_Options()
    {
        var input = TemplateInput("parameters.docx");
        input.Options = new InputOptions { EnforceEvenAmountOfPages = true };

        var ex = Assert.ThrowsAsync<NotSupportedException>(() => Mini.Create(input));
        Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.Options)));
    }

    [Test]
    public async Task Create_Accepts_Default_Options()
    {
        var input = TemplateInput("parameters.docx");
        input.Options = new InputOptions();

        using var output = await Mini.Create(input);

        // A default-initialised WordTemplateInput must not trip the unsupported-input guard.
        Assert.That(output.GetLength(), Is.GreaterThan(0));
    }

    [Test]
    public void Create_Rejects_A_Key_Used_Twice()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["logo"] = "text" };
        input.Images =
        [
            new WordImage
            {
                Name = "logo",
                File = ReadAsset("sample1.jpg")
            }
        ];

        var ex = Assert.ThrowsAsync<ArgumentException>(() => Mini.Create(input));
        Assert.That(ex!.Message, Does.Contain("logo"));
    }

    [Test]
    public async Task Create_Matches_Spaced_Tags_In_Collection_Rows_And_Across_Runs()
    {
        var input = new WordTemplateInput { Template = SpacedTagsTemplate().ToBinaryFile(ContentTypes.DOCX) };
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "Order 42" };
        input.CollectionParameters!.Add("Items", new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Name"] = "Pen" },
            new Dictionary<string, object> { ["Name"] = "Ink" }
        });

        var text = await Mini.GetText(input);

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("Order 42"));
            Assert.That(text, Does.Contain("Pen"));
            Assert.That(text, Does.Contain("Ink"));
            Assert.That(text, Does.Not.Contain("{{"));
        });
    }

    [Test]
    public async Task Create_Still_Takes_A_Key_Spelled_With_Spaces()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { [" title "] = "A spaced key" };

        var text = await Mini.GetText(input);

        Assert.That(text, Does.Contain("A spaced key"));
    }

    /// <summary>
    /// <c>{{ title }}</c> split over two runs the way Word saves an edited tag, and a table row holding
    /// <c>{{ Items.Name }}</c>.
    /// </summary>
    private static byte[] SpacedTagsTemplate()
    {
        using var stream = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(stream, WordprocessingDocumentType.Document))
        {
            var main = doc.AddMainDocumentPart();
            var row = new W.TableRow(new W.TableCell(new W.Paragraph(new W.Run(new W.Text("{{ Items.Name }}")))));
            main.Document = new W.Document(new W.Body(
                new W.Paragraph(
                    new W.Run(new W.Text("Title: {{ ") { Space = SpaceProcessingModeValues.Preserve }),
                    new W.Run(new W.Text("title }}"))),
                new W.Table(new W.TableProperties(), row),
                new W.Paragraph(new W.Run(new W.Text("End")))));
        }
        return stream.ToArray();
    }
}
