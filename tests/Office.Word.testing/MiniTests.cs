using DocumentFormat.OpenXml.Packaging;
using Office.Word.testing.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Mini;
using Regira.Office.Word.Models;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Office.Word.testing;

/// <summary>
/// Word.Mini runs the shared scenarios in <see cref="WordTestsBase"/> that its features reach; below is what MiniWord
/// does differently.
/// </summary>
[WordFixture]
[LeavesOut(WordFeature.Converting | WordFeature.Merging | WordFeature.PageImages | WordFeature.OtherFormats, "MiniWord has no layout engine")]
[LeavesOut(WordFeature.NestedDocuments | WordFeature.HeadersAndFooters | WordFeature.InputOptions, "MiniWord has no API for them: Create throws NotSupportedException")]
[LeavesOut(WordFeature.TitledTables | WordFeature.AltTextPictures, "MiniWord fills a {{Items.Name}} row and a {{logo}} tag instead")]
[LeavesOut(WordFeature.Bookmarks, "MiniWord reads .docx only, and the scenario's template is a .dot")]
[LeavesOut(WordFeature.HtmlParameters, "MiniWord writes an html_ value as text")]
[LeavesOut(WordFeature.DocumentBuilder, "Word.Mini has no DocumentBuilder")]
public class MiniTests() : WordTestsBase(new WordService(), "Mini")
{
    private WordService Mini => (WordService)Backend;

    // MiniWord reads .docx only
    public static IEnumerable<string> SourceFiles => ["multipage.docx"];


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
    public async Task Create_Rejects_DocumentParameters()
    {
        var input = TemplateInput("parameters.docx");
        input.DocumentParameters = new Dictionary<string, WordTemplateInput>
        {
            ["nested"] = TemplateInput("doc-1.docx")
        };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => Mini.Create(input));
        Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.DocumentParameters)));
    }

    [Test]
    public async Task Create_Rejects_Headers_And_Footers()
    {
        var input = TemplateInput("parameters.docx");
        input.Headers = [new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") }];
        input.Footers = [new WordHeaderFooterInput { Template = TemplateInput("add_footer.docx") }];

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => Mini.Create(input));
        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.Headers)));
            Assert.That(ex.Message, Does.Contain(nameof(WordTemplateInput.Footers)));
        });
    }

    [Test]
    public async Task Create_Rejects_Unsupported_Options()
    {
        var input = TemplateInput("parameters.docx");
        input.Options = new InputOptions { EnforceEvenAmountOfPages = true };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => Mini.Create(input));
        Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.Options)));
    }

    /// <summary>A default-initialised <see cref="WordTemplateInput"/> does not trip the unsupported-input guard.</summary>
    [Test]
    public async Task Create_Accepts_Default_Options()
    {
        var input = TemplateInput("parameters.docx");
        input.Options = new InputOptions();

        using var output = await Mini.Create(input);
        var facts = await ReadDocx(output);

        Assert.That(facts.BodyText, Does.Contain("Parameters"));
    }

    [Test]
    public async Task Create_Rejects_A_Key_Used_Twice()
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

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Mini.Create(input));
        Assert.That(ex!.Message, Does.Contain("logo"));
    }

    /// <summary>
    /// <c>{{ title }}</c> split over two runs the way Word saves an edited tag, and a table row holding
    /// <c>{{ Items.Name }}</c>.
    /// </summary>
    [Test]
    public async Task Create_Matches_Spaced_Tags_In_Collection_Rows_And_Across_Runs()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([Docx.SplitParagraph("Title: {{ ", "title }}"), Docx.Table(["{{ Items.Name }}"]), Docx.Paragraph("End")])
        };
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "Order 42" };
        input.CollectionParameters!.Add("Items", new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Name"] = "Pen" },
            new Dictionary<string, object> { ["Name"] = "Ink" }
        });

        using var output = await Mini.Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("Title: Order 42"));
            Assert.That(facts.TableRows, Is.EqualTo(new[] { 2 }), "a row per item");
            Assert.That(facts.BodyText, Does.Contain("Pen").And.Contain("Ink"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    [Test]
    public async Task Create_Still_Takes_A_Key_Spelled_With_Spaces()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { [" title "] = "A spaced key" };

        using var output = await Mini.Create(input);
        var facts = await ReadDocx(output);

        Assert.That(facts.BodyText, Does.Contain("A spaced key"));
    }

    /// <summary>
    /// A collection's key and its fields are matched literally too: <c>{{ Line Items.Price (excl. VAT) }}</c> reads the
    /// field <c>Price (excl. VAT)</c> of the collection <c>Line Items</c>.
    /// </summary>
    [Test]
    public async Task Create_Matches_A_Collection_Key_And_Field_Literally()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([Docx.Table(["{{ Line Items.Price (excl. VAT) }}"], ["{{ Line Items.Name }}"])])
        };
        input.CollectionParameters!.Add("Line Items", new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Name"] = "Pen", ["Price (excl. VAT)"] = "1.50" },
            new Dictionary<string, object> { ["Name"] = "Ink", ["Price (excl. VAT)"] = "2.00" }
        });

        using var output = await Mini.Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("Pen").And.Contain("1.50"));
            Assert.That(facts.BodyText, Does.Contain("Ink").And.Contain("2.00"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    /// <summary>
    /// A collection under a key MiniWord cannot take reads as one under a key it can: no rows remove the template row,
    /// and a field no row supplies is left empty.
    /// </summary>
    [TestCase("Items")]
    [TestCase("Order Lines")]
    public async Task A_Collection_Reads_The_Same_Under_Any_Key(string key)
    {
        WordTemplateInput Input(params IDictionary<string, object>[] rows)
        {
            var input = new WordTemplateInput
            {
                Template = Docx.Document([Docx.Paragraph("Intro"), Docx.Table([$"{{{{{key}.Name}}}}"], [$"{{{{{key}.Note}}}}"]), Docx.Paragraph("End")])
            };
            input.CollectionParameters!.Add(key, rows.ToList());
            return input;
        }

        using var empty = await Mini.Create(Input());
        using var withoutNote = await Mini.Create(Input(new Dictionary<string, object> { ["Name"] = "Pen" }, new Dictionary<string, object> { ["Name"] = "Ink" }));
        var emptyFacts = await ReadDocx(empty, "-empty");
        var withoutNoteFacts = await ReadDocx(withoutNote, "-without-note");

        Assert.Multiple(() =>
        {
            Assert.That(emptyFacts.TableRows.Sum(), Is.Zero, "the template row goes with an empty collection");
            Assert.That(emptyFacts.Leftovers, Is.Empty);
            Assert.That(withoutNoteFacts.TableRows.Sum(), Is.EqualTo(2));
            Assert.That(withoutNoteFacts.BodyText, Does.Contain("Pen").And.Contain("Ink"));
            Assert.That(withoutNoteFacts.Leftovers, Is.Empty, "a field no row supplies is left empty");
        });
    }

    /// <summary>A collection's tag reads its field with spaces around the dot too, whatever the field's key holds.</summary>
    [TestCase("Name")]
    [TestCase("Unit Price")]
    public async Task A_Collection_Tag_Spaced_Around_Its_Dot_Reads_The_Field(string field)
    {
        var input = new WordTemplateInput { Template = Docx.Document([Docx.Table([$"{{{{Items . {field}}}}}"])]) };
        input.CollectionParameters!.Add("Items", new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { [field] = "Pen" },
            new Dictionary<string, object> { [field] = "Ink" }
        });

        using var output = await Mini.Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.TableRows.Sum(), Is.EqualTo(2));
            Assert.That(facts.BodyText, Does.Contain("Pen").And.Contain("Ink"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    [Test]
    public async Task Create_Rejects_A_Key_That_Reads_As_A_Collection_Field()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["Items.Name"] = "text" };
        input.CollectionParameters!.Add("Items", new List<IDictionary<string, object>>
        {
            new Dictionary<string, object> { ["Name"] = "Pen" }
        });

        var ex = await Assert.ThrowsAsync<ArgumentException>(() => Mini.Create(input));
        Assert.That(ex!.Message, Does.Contain("Items.Name"));
    }

    /// <summary>
    /// A marker paragraph that stores the section break is emptied rather than removed, so the break stays.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Section_Break_On_A_Marker_Paragraph_Survives(bool isDraft)
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("Intro"),
                Docx.Paragraph("{{#if IsDraft}}"),
                Docx.Paragraph("DRAFTTEXT"),
                Docx.SectionBreak("{{/if}}"),
                Docx.Paragraph("Outro")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = isDraft }
        };

        using var output = await Mini.Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Sections, Has.Count.EqualTo(2), "the break and the final section");
            Assert.That(facts.BodyText, Does.Not.Contain("{{"));
            Assert.That(facts.BodyText, Does.Contain("Intro").And.Contain("Outro"));
            Assert.That(facts.BodyText, isDraft ? Does.Contain("DRAFTTEXT") : Does.Not.Contain("DRAFTTEXT"));
        });
    }

    /// <summary>A content control holding a section break ends its section, so a block cannot span it.</summary>
    [Test]
    public async Task A_Block_Across_A_Section_Break_In_A_Content_Control_Fails()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("{{#if IsDraft}}"),
                new W.SdtBlock(new W.SdtProperties(new W.SdtId { Val = 1 }), new W.SdtContentBlock(Docx.SectionBreak("Chapter 1"))),
                Docx.Paragraph("{{/if}}")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = false }
        };

        var ex = await Assert.ThrowsAsync<FormatException>(() => Mini.Create(input));

        Assert.That(ex!.Message, Does.Contain("section"));
    }

    /// <summary>A container that ends as the template wrote it — here in a content control — gets no paragraph added.</summary>
    [Test]
    public async Task A_Container_Ending_As_The_Template_Wrote_It_Is_Left_As_It_Is()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("{{#if IsDraft}}"),
                Docx.Paragraph("DRAFTTEXT"),
                Docx.Paragraph("{{/if}}"),
                Docx.ContentControl("Signature")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = true }
        };

        using var output = await Mini.Create(input);
        await ReadDocx(output);
        using var doc = WordprocessingDocument.Open(new MemoryStream(output.GetBytes()!), false);
        var body = doc.MainDocumentPart!.Document!.Body!;

        Assert.That(body.ChildElements.Last(child => child is not W.SectionProperties), Is.InstanceOf<W.SdtBlock>());
    }
}
