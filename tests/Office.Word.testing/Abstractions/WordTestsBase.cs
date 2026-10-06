using DocumentFormat.OpenXml.Packaging;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;
using Regira.Utilities;
using System.Globalization;
using System.Text.Json;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Office.Word.testing.Abstractions;

/// <summary>
/// Shared scenarios for every Word backend.
/// <para>
/// A backend fixture is marked <see cref="WordFixtureAttribute"/> and gets every scenario here, apart from those that
/// <see cref="NeedsAttribute">need</see> a feature it <see cref="LeavesOutAttribute">leaves out</see>. It overrides a
/// scenario only where its backend behaves differently, and restates the scenario's attributes when it does. A
/// scenario with cases that differ per backend names a static member the fixture declares: <c>SourceFiles</c>
/// (<see cref="From_File"/>) and <c>OutputFormats</c> (<see cref="Convert_To"/>).
/// </para>
/// <para>
/// A scenario reads what it produced with <see cref="DocxFacts"/>, <see cref="PdfFacts"/> or <see cref="ImageFacts"/>,
/// never with the backend under test, and saves it in <c>Assets/Output/{Backend}</c> under its own name, the facts
/// beside it.
/// </para>
/// </summary>
public abstract class WordTestsBase : WordAssetsTestsBase
{
    protected readonly IWordCreator? Creator;
    protected readonly IWordConverter? Converter;
    protected readonly IWordMerger? Merger;
    protected readonly IWordTextExtractor? TextExtractor;
    protected readonly IWordImageExtractor? ImageExtractor;
    protected readonly IWordToImagesService? ToImagesService;

    /// <param name="service">
    /// The backend. Typed <see cref="object"/> because no single Word interface is common to every
    /// backend, and one overload per interface would be ambiguous for a backend that implements several.
    /// </param>
    /// <param name="outputFolderName">The backend's folder under <c>Assets/Output</c>.</param>
    protected WordTestsBase(object service, string outputFolderName) : base(outputFolderName)
    {
        Backend = service;
        Creator = service as IWordCreator;
        Converter = service as IWordConverter;
        Merger = service as IWordMerger;
        TextExtractor = service as IWordTextExtractor;
        ImageExtractor = service as IWordImageExtractor;
        ToImagesService = service as IWordToImagesService;
    }

    /// <summary>
    /// The backend as the fixture handed it over, for casting to its concrete type.
    /// </summary>
    protected object Backend { get; }

    /// <summary>
    /// The backend as a full <see cref="IWordService"/>. Only valid for a fixture whose backend
    /// implements the composite; a partial backend uses the individual capabilities instead.
    /// </summary>
    protected IWordService Service => (IWordService)Backend;

    /// <summary>A <c>Convert_To</c> case: the format asked for and the content type the output must carry.</summary>
    protected static TestCaseData Output(FileFormat format, string contentType)
        => new TestCaseData(format, contentType).SetArgDisplayNames(format.ToString());

    /// <summary>
    /// Builds a document with the backend's own <c>DocumentBuilder</c>: the inputs it loads, joined as
    /// <paramref name="merge"/> asks, then the paragraphs and headers, on pages of the size and orientation
    /// <paramref name="settings"/> gives. A fixture that does not leave out <see cref="WordFeature.DocumentBuilder"/>
    /// overrides this.
    /// </summary>
    protected virtual Task<IMemoryFile> Build(IEnumerable<Paragraph> paragraphs, IEnumerable<WordHeaderFooterInput> headers, ConversionOptions? conversion = null,
        WordTemplateInput[]? inputs = null, MergeOptions? merge = null, DocumentSettings? settings = null)
    {
        Assert.Fail($"{GetType().Name} does not leave out {nameof(WordFeature)}.{nameof(WordFeature.DocumentBuilder)}, so it overrides {nameof(Build)}.");
        return null!;
    }


    // ---- creating ----

    /// <summary>A template in another format is read whole: its text, tables and pictures come through.</summary>
#pragma warning disable NUnit1011 // the member is the fixture's: each backend declares its own SourceFiles
    [TestCaseSource("SourceFiles")]
#pragma warning restore NUnit1011
    [Needs(WordFeature.Creating)]
    public virtual async Task From_File(string filename)
    {
        // the binary and ODF templates are saved copies of template.docx
        var reference = DocxFacts.Read(ReadAsset(Path.GetExtension(filename) == ".docx" ? filename : "template.docx"));

        using var output = await RequireCreator().Create(TemplateInput(filename));
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(Facts.Sniff(output.GetBytes()!), Is.EqualTo(FileFormat.Docx));
            Assert.That(facts.TableRows, Is.EqualTo(reference.TableRows));
            Assert.That(facts.Pictures.Select(picture => (picture.Width, picture.Height)), Is.SupersetOf(reference.Pictures.Select(picture => (picture.Width, picture.Height))));
            foreach (var excerpt in Excerpts(reference))
            {
                Assert.That(facts.BodyText, Does.Contain(excerpt));
            }
        });
    }

    [Test]
    [Needs(WordFeature.Creating | WordFeature.Bookmarks)]
    public virtual async Task Bookmarks()
    {
        var pi = Math.Round(Math.PI, 10).ToString(CultureInfo.InvariantCulture);
        var input = TemplateInput("bookmarks.dot");
        input.GlobalParameters = new Dictionary<string, object> { ["rs_Pi"] = pi };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.That(facts.BodyText, Does.Contain(pi));
    }

    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task Replace_Parameters()
    {
        var parameters = new Dictionary<string, object>
        {
            ["title"] = "A title for this word document",
            ["date"] = DateTime.Today.ToShortDateString()
        };
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = parameters;

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            foreach (var value in parameters.Values)
            {
                Assert.That(facts.BodyText, Does.Contain(value.ToString()));
            }
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    /// <summary>The picture whose Alt Text is the image's name is replaced: its bytes are the image's, not the placeholder's.</summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.AltTextPictures)]
    public virtual async Task Replace_Image()
    {
        var input = TemplateInput("template_image.docx");
        input.Images!.Add(new WordImage { Name = "placeholder", File = ReadAsset("sample1.jpg") });

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        var hashes = facts.Pictures.Select(picture => picture.Sha1).ToList();
        Assert.Multiple(() =>
        {
            Assert.That(hashes, Does.Contain(Asset("sample1.jpg").Sha1));
            Assert.That(hashes, Does.Not.Contain(Asset("placeholder.png").Sha1), "the template's placeholder picture is gone");
        });
    }

    /// <summary>The collection fills the table whose Alt Text title is its key: its template row becomes one row per item.</summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.TitledTables)]
    public virtual async Task Template_Row()
    {
        var input = TemplateInput("template_row.docx");
        input.CollectionParameters!.Add("Template_Table", TemplateRows());

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            // a heading row and the template row: the heading row stays, the template row becomes three
            Assert.That(facts.TableRows, Is.EqualTo(new[] { 2, 4 }));
            Assert.That(Order(facts.BodyText, "Item #10", "Item #12", "Item #31"), Is.Ordered.And.All.GreaterThanOrEqualTo(0));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    [Test]
    [Needs(WordFeature.Creating | WordFeature.NestedDocuments | WordFeature.TitledTables | WordFeature.AltTextPictures)]
    public virtual async Task Nested_Documents()
    {
        var doc1 = TemplateInput("template_row.docx");
        doc1.CollectionParameters!.Add("Template_Table", TemplateRows());

        var doc2 = TemplateInput("template_image.docx");
        doc2.Options = new InputOptions { InheritFont = true };
        doc2.Images!.Add(new WordImage { Name = "placeholder", File = ReadAsset("sample1.jpg") });

        var input = TemplateInput("nested_templates.docx");
        input.DocumentParameters = new Dictionary<string, WordTemplateInput>
        {
            ["doc1"] = doc1,
            ["doc2"] = doc2
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("Item #12"));
            Assert.That(facts.TableRows, Is.EqualTo(new[] { 2, 4 }), "doc1's tables, its collection filled");
            Assert.That(facts.Pictures.Select(picture => picture.Sha1), Does.Contain(Asset("sample1.jpg").Sha1), "doc2's picture replaced");
            Assert.That(facts.Leftovers, Is.EqualTo(new[] { "<{doc3}>" }), "the template's one placeholder no document is given for");
        });
    }

    /// <summary>
    /// The self-inclusion guard is per call: one long-lived instance keeps inserting documents past the nesting limit
    /// (100), summed over its calls — 51 calls of two nested documents each.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.NestedDocuments)]
    public virtual async Task Nested_Documents_Do_Not_Wear_Out_The_Service()
    {
        var creator = RequireCreator();
        for (var i = 0; i < 51; i++)
        {
            var input = new WordTemplateInput
            {
                Template = Docx.Document("<{ doc1 }>", "<{ doc2 }>"),
                DocumentParameters = new Dictionary<string, WordTemplateInput>
                {
                    ["doc1"] = new() { Template = Docx.Document("ONE") },
                    ["doc2"] = new() { Template = Docx.Document("TWO") }
                }
            };

            using var output = await creator.Create(input);
        }
    }

    [Test]
    [Needs(WordFeature.Creating | WordFeature.NestedDocuments | WordFeature.HeadersAndFooters)]
    public virtual async Task A_Template_That_Includes_Itself_Fails()
    {
        var creator = RequireCreator();
        var nested = new WordTemplateInput { Template = Docx.Document("<{ doc1 }>") };
        nested.DocumentParameters!["doc1"] = nested;
        var header = new WordTemplateInput { Template = Docx.Document("Body") };
        header.Headers!.Add(new WordHeaderFooterInput { Template = header });

        var viaNesting = await Assert.ThrowsAsync<InvalidOperationException>(() => creator.Create(nested));
        var viaHeader = await Assert.ThrowsAsync<InvalidOperationException>(() => creator.Create(header));

        Assert.Multiple(() =>
        {
            Assert.That(viaNesting!.Message, Does.Contain("Maximum insertable documents"));
            Assert.That(viaHeader!.Message, Does.Contain("Maximum insertable documents"));
        });
    }

    [Test]
    [Needs(WordFeature.Creating | WordFeature.TitledTables)]
    public virtual async Task A_Missing_Collection_Table_Leaves_The_Others()
    {
        var input = TemplateInput("template_row.docx");
        input.CollectionParameters!.Add("Not_In_The_Template", TemplateRows());
        input.CollectionParameters!.Add("Template_Table", TemplateRows());

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.That(facts.BodyText, Does.Contain("Item #12"));
    }

    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Parameter_Key_Is_Matched_Literally()
    {
        // characters a regular expression would read as syntax
        var input = new WordTemplateInput { Template = Docx.Document("Sum: {{ Total (EUR) }}, item {{ x[1 }}, rate {{ a.b }}") };
        input.GlobalParameters = new Dictionary<string, object>
        {
            ["Total (EUR)"] = "42",
            ["x[1"] = "one",
            ["a.b"] = "0.5"
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.That(facts.BodyText, Does.Contain("Sum: 42, item one, rate 0.5"));
    }

    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Null_Or_Unused_Parameter_Is_Harmless()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object>
        {
            ["title"] = null!,
            ["date"] = "16/09/2026",
            ["html_not_in_the_template"] = "<b>unused</b>"
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Leftovers, Is.Empty, "a null value is written as empty text");
            Assert.That(facts.BodyText, Does.Contain("16/09/2026"));
        });
    }

    /// <summary><c>&amp;p</c>, <c>&amp;l</c> and <c>$1</c> mean something to some replace APIs; a parameter value does not.</summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Parameter_Value_Is_Inserted_Verbatim()
    {
        const string value = "Tom & Jerry &p &l $1";
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = value };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.That(facts.BodyText, Does.Contain(value));
    }

    /// <summary>A line ending in a value becomes a soft line break within its paragraph, not a literal control character.</summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Multiline_Parameter_Becomes_A_Line_Break()
    {
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "First line\r\nSecond line" };

        using var output = await RequireCreator().Create(input);
        await ReadDocx(output);

        using var doc = WordprocessingDocument.Open(new MemoryStream(output.GetBytes()!), false);
        var paragraph = doc.MainDocumentPart!.Document!.Body!.Descendants<W.Paragraph>().Single(p => p.InnerText.Contains("First line"));
        Assert.Multiple(() =>
        {
            Assert.That(paragraph.InnerText, Does.Contain("Second line"));
            Assert.That(paragraph.Descendants<W.Break>().Any(b => b.Type == null || b.Type.Value == W.BreakValues.TextWrapping), Is.True);
            Assert.That(paragraph.InnerText, Does.Not.Contain("\v").And.Not.Contain("\r"));
        });
    }

    /// <summary>A parameter whose key starts with <c>html_</c> is converted from HTML, not written as markup.</summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.HtmlParameters)]
    public virtual async Task An_Html_Parameter_Is_Converted()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document("Notes: {{ html_notes }}", "After the notes"),
            GlobalParameters = new Dictionary<string, object> { ["html_notes"] = "<p>This is <strong>bold</strong> text.</p>" }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("This is bold text."));
            Assert.That(facts.BodyText, Does.Contain("After the notes"));
            Assert.That(facts.BodyText, Does.Not.Contain("<strong>"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    /// <summary>Parameters sent as JSON arrive as <see cref="JsonElement"/> values once System.Text.Json reads them.</summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.TitledTables)]
    public virtual async Task Parameters_Read_From_Json_Fill_The_Template()
    {
        const string json = """
            {
                "collectionParameters": {
                    "Template_Table": [
                        { "id": "10", "title": "Item #10", "price": 100 },
                        { "id": "12", "title": "Item #12", "price": 200 },
                        { "id": "31", "title": "Item #31", "price": 350 }
                    ]
                }
            }
            """;
        var input = JsonSerializer.Deserialize<WordTemplateInput>(json, new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        input.Template = ReadAsset("template_row.docx");

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.TableRows, Is.EqualTo(new[] { 2, 4 }));
            Assert.That(facts.BodyText, Does.Contain("Item #31"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    /// <summary>A whole input read from a JSON file: the template's bytes, Base64-encoded, and its parameters.</summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task An_Input_Read_From_A_Json_File_Fills_The_Template()
    {
        var model = JsonSerializer.Deserialize<JsonInput>(await File.ReadAllTextAsync(InputPath("json-input.json")), new JsonSerializerOptions { PropertyNameCaseInsensitive = true })!;
        var input = new WordTemplateInput { Template = model.TemplateBytes.ToBinaryFile(), GlobalParameters = model.GlobalParameters };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain(model.GlobalParameters["heading1"].ToString()));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    private sealed record JsonInput(byte[] TemplateBytes, Dictionary<string, object> GlobalParameters);


    // ---- headers and footers ----

    [Test]
    [Needs(WordFeature.Creating | WordFeature.HeadersAndFooters)]
    public virtual Task Add_Header_And_Footer() => AddHeaderAndFooter(HeaderFooterType.Default);

    /// <summary>The header and footer go to the first page's own stories, which the section switches on.</summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.HeadersAndFooters)]
    public virtual Task Add_FirstPage_Header_And_Footer() => AddHeaderAndFooter(HeaderFooterType.FirstPage);

    private async Task AddHeaderAndFooter(HeaderFooterType type)
    {
        var input = TemplateInput("lorem_ipsum.docx");
        input.Headers!.Add(new WordHeaderFooterInput { Template = TemplateInput("add_header.docx"), Type = type });
        input.Footers!.Add(new WordHeaderFooterInput { Template = TemplateInput("add_footer.docx"), Type = type });
        var story = type == HeaderFooterType.FirstPage ? "first" : "default";

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Headers.Where(header => header.Type == story).Select(header => header.Text), Has.Some.Contains("Header"));
            Assert.That(facts.Footers.Where(footer => footer.Type == story).Select(footer => footer.Text), Has.Some.Contains("Testing by"));
            Assert.That(facts.Sections[0].TitlePage, Is.EqualTo(type == HeaderFooterType.FirstPage));
            // the header and footer templates' body text does not leak into the document
            Assert.That(facts.BodyText, Does.Not.Contain("Jibberisch"));
        });
    }

    /// <summary>
    /// A first-page or even-page version of one story switches those pages to stories of their own; the other story,
    /// given only a default version, must still appear on them — page numbers in a footer, say, under an even header.
    /// </summary>
    /// <param name="type"><see cref="HeaderFooterType.FirstPage"/> (page 1) or <see cref="HeaderFooterType.Even"/> (page 2)</param>
    /// <param name="specialHeader">Whether the header gets the special version, rather than the footer</param>
    [TestCase(HeaderFooterType.Even, true)]
    [TestCase(HeaderFooterType.Even, false)]
    [TestCase(HeaderFooterType.FirstPage, true)]
    [TestCase(HeaderFooterType.FirstPage, false)]
    [Needs(WordFeature.Converting | WordFeature.HeadersAndFooters)]
    public virtual async Task A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(HeaderFooterType type, bool specialHeader)
    {
        var input = new WordTemplateInput { Template = Docx.Document([Docx.PageBreakAfter("One"), Docx.Paragraph("Two")]) };
        var given = specialHeader ? input.Headers! : input.Footers!;
        var other = specialHeader ? input.Footers! : input.Headers!;
        given.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = Docx.Document("GIVENDEFAULT") } });
        given.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = Docx.Document("GIVENSPECIAL") }, Type = type });
        other.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = Docx.Document("OTHERDEFAULT") } });

        using var pdf = await RequireConverter().Convert(input, FileFormat.Pdf);
        var pages = (await ReadPdf(pdf)).PageTexts;
        var special = type == HeaderFooterType.FirstPage ? 0 : 1;
        var regular = 1 - special;

        Assert.That(pages, Has.Count.GreaterThanOrEqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(pages[special], Does.Contain("GIVENSPECIAL"));
            Assert.That(pages[special], Does.Contain("OTHERDEFAULT"), "the story given no special version must stay on the special page");
            Assert.That(pages[regular], Does.Contain("GIVENDEFAULT"));
            Assert.That(pages[regular], Does.Contain("OTHERDEFAULT"));
        });
    }


    // ---- merging ----

    /// <summary>Both documents come through in order, and a header and footer added to the merge reach it.</summary>
    [Test]
    [Needs(WordFeature.Merging | WordFeature.Creating | WordFeature.HeadersAndFooters)]
    public virtual async Task Merge()
    {
        using var merged = await RequireMerger().Merge(new[] { "doc-1.docx", "doc-2.docx" }.Select(TemplateInput));

        var input = new WordTemplateInput { Template = merged.ToBinaryFile() };
        input.Headers!.Add(new WordHeaderFooterInput { Template = TemplateInput("header_footer.docx") });
        input.Footers!.Add(new WordHeaderFooterInput { Template = TemplateInput("header_footer.docx") });
        var headerFooter = DocxFacts.Read(ReadAsset("header_footer.docx"));

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(Order(facts.BodyText, "Vertical images", "Some text in Arial"), Is.Ordered.And.All.GreaterThanOrEqualTo(0), "doc-1, then doc-2");
            Assert.That(facts.TableRows, Is.EqualTo(DocxFacts.Read(ReadAsset("doc-1.docx")).TableRows));
            Assert.That(facts.Pictures, Has.Count.GreaterThanOrEqualTo(3), "doc-1's two pictures and doc-2's one");
            Assert.That(facts.Headers.Select(header => header.Text), Has.Some.Contains(FirstLine(headerFooter.Headers)));
            Assert.That(facts.Footers.Select(footer => footer.Text), Has.Some.Contains(FirstLine(headerFooter.Footers)));
        });
    }

    /// <summary>
    /// A merged document starts on a new page, and keeps its own section breaks: BRAVO's second section starts a page of
    /// its own, as it does in BRAVO.
    /// </summary>
    [Test]
    [Needs(WordFeature.Merging | WordFeature.Converting)]
    public virtual async Task Merged_Documents_Start_On_A_New_Page()
    {
        using var merged = await RequireMerger().Merge(MergeInputs());
        var facts = await ReadDocx(merged);
        using var pdf = await RequireConverter().Convert(new WordTemplateInput { Template = merged }, FileFormat.Pdf);
        var pages = await ReadPdf(pdf);

        // three pages are enough to tell: the FreeSpire.Doc free edition writes no more
        Assert.That(pages.Pages, Is.GreaterThanOrEqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(facts.Sections.Skip(1).Select(section => section.Start), Is.EqualTo(new[] { "nextPage", "nextPage", "nextPage" }));
            Assert.That(pages.PageTexts[0], Does.Contain("ALPHA").And.Not.Contain("BRAVO"));
            Assert.That(pages.PageTexts[1], Does.Contain("BRAVOFIRST").And.Not.Contain("BRAVOLAST"));
            Assert.That(pages.PageTexts[2], Does.Contain("BRAVOLAST"));
        });
    }

    /// <summary>
    /// Asked to (<see cref="MergeOptions.FollowOn"/>), a merged document follows on from the previous one's last page,
    /// and still keeps its own section breaks: BRAVO's second section starts a page of its own.
    /// </summary>
    [Test]
    [Needs(WordFeature.Merging | WordFeature.Converting)]
    public virtual async Task Merged_Documents_Follow_On_From_The_Previous_Page_When_Asked()
    {
        using var merged = await RequireMerger().Merge(MergeInputs(), new MergeOptions { FollowOn = true });
        var facts = await ReadDocx(merged);
        using var pdf = await RequireConverter().Convert(new WordTemplateInput { Template = merged }, FileFormat.Pdf);
        var pages = await ReadPdf(pdf);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Sections.Skip(1).Select(section => section.Start), Is.EqualTo(new[] { "continuous", "nextPage", "continuous" }));
            Assert.That(pages.Pages, Is.EqualTo(2));
            Assert.That(pages.PageTexts[0], Does.Contain("ALPHA").And.Contain("BRAVOFIRST"));
            Assert.That(pages.PageTexts[1], Does.Contain("BRAVOLAST").And.Contain("CHARLIE"));
        });
    }

    /// <summary>ALPHA; BRAVO, in two sections; CHARLIE.</summary>
    private static WordTemplateInput[] MergeInputs() =>
    [
        new() { Template = Docx.Document("ALPHA") },
        new() { Template = Docx.Document([Docx.SectionBreak("BRAVOFIRST"), Docx.Paragraph("BRAVOLAST")]) },
        new() { Template = Docx.Document("CHARLIE") }
    ];


    // ---- input options ----

    /// <summary>A document of an odd page count is padded, so the next merged document starts on an odd page.</summary>
    [Test]
    [Needs(WordFeature.Merging | WordFeature.Converting | WordFeature.InputOptions)]
    public virtual async Task EnforceEvenAmountOfPages_Pads_An_Odd_Document()
    {
        var inputs = new WordTemplateInput[]
        {
            new() { Template = Docx.Document("ALPHA"), Options = new InputOptions { EnforceEvenAmountOfPages = true } },
            new() { Template = Docx.Document("BRAVO") }
        };

        using var merged = await RequireMerger().Merge(inputs);
        using var pdf = await RequireConverter().Convert(new WordTemplateInput { Template = merged }, FileFormat.Pdf);
        var pages = await ReadPdf(pdf);

        Assert.That(pages.Pages, Is.GreaterThanOrEqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(pages.PageTexts[0], Does.Contain("ALPHA"));
            Assert.That(pages.PageTexts[1], Does.Not.Contain("ALPHA").And.Not.Contain("BRAVO"), "the padding page");
            Assert.That(pages.PageTexts[2], Does.Contain("BRAVO"));
        });
    }

    /// <summary>
    /// A padded input starts on an odd page, not just a new one, and so does the input after it: its pages are then
    /// counted as they come out, and its padding page is the only blank one before the next input.
    /// </summary>
    [Test]
    [Needs(WordFeature.Merging | WordFeature.Converting | WordFeature.InputOptions)]
    public virtual async Task EnforceEvenAmountOfPages_Starts_A_Padded_Input_On_An_Odd_Page()
    {
        var inputs = new WordTemplateInput[]
        {
            new() { Template = Docx.Document("ALPHA") },
            new() { Template = Docx.Document("BRAVO"), Options = new InputOptions { EnforceEvenAmountOfPages = true } },
            new() { Template = Docx.Document("CHARLIE") }
        };

        using var merged = await RequireMerger().Merge(inputs);
        var facts = await ReadDocx(merged);
        using var pdf = await RequireConverter().Convert(new WordTemplateInput { Template = merged }, FileFormat.Pdf);
        var pages = await ReadPdf(pdf);

        Assert.That(pages.Pages, Is.GreaterThanOrEqualTo(3));
        Assert.Multiple(() =>
        {
            Assert.That(facts.Sections.Skip(1).Select(section => section.Start), Is.EqualTo(new[] { "oddPage", "oddPage" }), "BRAVO and CHARLIE");
            Assert.That(pages.PageTexts[0], Does.Contain("ALPHA").And.Not.Contain("BRAVO"));
            Assert.That(pages.PageTexts[1], Does.Not.Contain("ALPHA").And.Not.Contain("BRAVO"), "the blank page before BRAVO's odd page");
            Assert.That(pages.PageTexts[2], Does.Contain("BRAVO"));
            // CHARLIE on page 5, after BRAVO's padding page, where the PDF shows it: FreeSpire.Doc's free edition writes three pages
            if (pages.Pages >= 5)
            {
                Assert.That(pages.PageTexts[3], Does.Not.Contain("BRAVO").And.Not.Contain("CHARLIE"), "BRAVO's padding page");
                Assert.That(pages.PageTexts[4], Does.Contain("CHARLIE"));
            }
        });
    }

    /// <summary>
    /// A merged input that takes the first input's font is padded as it comes out in that font, once: 50 lines that
    /// fill one page at their own 8 pt take two at the first input's 20 pt, which need no padding.
    /// </summary>
    [Test]
    [Needs(WordFeature.Merging | WordFeature.InputOptions)]
    public virtual async Task EnforceEvenAmountOfPages_Pads_An_Input_In_The_Font_It_Inherits()
    {
        WordTemplateInput[] Inputs(bool inherit) =>
        [
            new() { Template = Docx.Document(Docx.Paragraphs("ALPHA"), normalFontSize: 40) },
            new()
            {
                Template = Docx.Document(Docx.Paragraphs(Enumerable.Range(1, 50).Select(line => $"BRAVO {line}").ToArray()), normalFontSize: 16),
                Options = new InputOptions { InheritFont = inherit, EnforceEvenAmountOfPages = true }
            }
        ];

        using var own = await RequireMerger().Merge(Inputs(false));
        using var inherited = await RequireMerger().Merge(Inputs(true));
        var ownFacts = await ReadDocx(own, "-own");
        var inheritedFacts = await ReadDocx(inherited, "-inherited");

        Assert.Multiple(() =>
        {
            Assert.That(ownFacts.PageBreaks, Is.EqualTo(1), "one page in its own font: padded");
            Assert.That(inheritedFacts.PageBreaks, Is.Zero, "two pages in the inherited font: not padded");
        });
    }

    [Test]
    [Needs(WordFeature.Creating | WordFeature.InputOptions)]
    public virtual async Task RemoveEmptyParagraphs_Removes_Them()
    {
        WordTemplateInput Input(bool remove) => new()
        {
            Template = Docx.Document([Docx.Paragraph("A"), Docx.EmptyParagraph(), Docx.EmptyParagraph(), Docx.Paragraph("B"), Docx.EmptyParagraph()]),
            Options = new InputOptions { RemoveEmptyParagraphs = remove }
        };

        using var kept = await RequireCreator().Create(Input(false));
        using var removed = await RequireCreator().Create(Input(true));
        var keptFacts = await ReadDocx(kept, "-kept");
        var removedFacts = await ReadDocx(removed, "-removed");

        Assert.Multiple(() =>
        {
            Assert.That(keptFacts.EmptyParagraphs, Is.EqualTo(3), "without the option");
            Assert.That(removedFacts.EmptyParagraphs, Is.Zero);
            Assert.That(removedFacts.BodyText, Does.Contain("A").And.Contain("B"));
        });
    }

    [Test]
    [Needs(WordFeature.Creating | WordFeature.InputOptions)]
    public virtual async Task HorizontalAlignment_Aligns_The_Normal_Paragraphs()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document("FIRSTPARAGRAPH", "SECONDPARAGRAPH"),
            Options = new InputOptions { HorizontalAlignment = HorizontalAlignment.Justify }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.That(facts.Paragraphs.Where(paragraph => paragraph.Text.EndsWith("PARAGRAPH")).Select(paragraph => paragraph.Alignment),
            Is.EqualTo(new[] { "both", "both" }));
    }

    /// <summary>A merged input that takes the first input's font keeps the alignment it asks for.</summary>
    [Test]
    [Needs(WordFeature.Merging | WordFeature.InputOptions)]
    public virtual async Task HorizontalAlignment_Holds_With_An_Inherited_Font()
    {
        var inputs = new WordTemplateInput[]
        {
            new() { Template = Docx.Document(Docx.Paragraphs("ALPHA"), normalFontSize: 40) },
            new()
            {
                Template = Docx.Document(Docx.Paragraphs("BRAVO"), normalFontSize: 16),
                Options = new InputOptions { InheritFont = true, HorizontalAlignment = HorizontalAlignment.Center }
            }
        };

        using var merged = await RequireMerger().Merge(inputs);
        var facts = await ReadDocx(merged);

        Assert.That(facts.Paragraphs.Where(paragraph => paragraph.Text == "BRAVO").Select(paragraph => paragraph.Alignment), Is.EqualTo(new[] { "center" }));
    }

    /// <summary>
    /// A nested document's Normal text takes the template's Normal font size — 11 pt in <c>parent-template.docx</c> —
    /// where it keeps its own, 9 pt and 10 pt, without the option.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.NestedDocuments | WordFeature.InputOptions)]
    public virtual async Task InheritFont_Sets_A_Nested_Document_In_The_Templates_Font()
    {
        WordTemplateInput Input(bool inherit) => new()
        {
            Template = ReadAsset("parent-template.docx"),
            DocumentParameters = new Dictionary<string, WordTemplateInput>
            {
                ["ExternDoc"] = new() { Template = ReadAsset("extern-small-font.docx"), Options = new InputOptions { InheritFont = inherit } }
            }
        };
        var nestedTexts = DocxFacts.Read(ReadAsset("extern-small-font.docx")).Paragraphs.Select(paragraph => paragraph.Text).Where(text => text.Length > 0).ToHashSet();

        using var own = await RequireCreator().Create(Input(false));
        using var inherited = await RequireCreator().Create(Input(true));
        IEnumerable<int> NestedSizes(DocxFacts facts) => facts.Paragraphs.Where(paragraph => nestedTexts.Contains(paragraph.Text)).SelectMany(paragraph => paragraph.RunSizes);
        var ownSizes = NestedSizes(await ReadDocx(own, "-own")).ToList();
        var inheritedSizes = NestedSizes(await ReadDocx(inherited, "-inherited")).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(MostCommon(ownSizes), Is.EqualTo(18), "without the option, the nested document's own 9 pt");
            Assert.That(MostCommon(inheritedSizes), Is.EqualTo(22), "the template's Normal 11 pt");
        });
    }


    // ---- realistic templates ----

    /// <summary>
    /// An invoice: dotted parameters in text boxes, a collection table, a header and a footer. Read whole, every copy of a
    /// text box included, nothing of the template's syntax is left.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.TitledTables)]
    public virtual async Task An_Invoice_Is_Filled()
    {
        var input = TemplateInput(Path.Combine("Factuur", "factuur.docx"));
        input.GlobalParameters = DictionaryUtility.Flatten(DictionaryUtility.ToDictionary(Invoice))!;
        input.CollectionParameters!.Add("invoiceLines", InvoiceLines());

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain(Invoice.customer.title));
            Assert.That(facts.BodyText, Does.Contain("Overdracht gegevens - opstellen en bijwerken testomgeving"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    /// <summary>The invoice again, assembled from nested templates — the header and footer among them.</summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.TitledTables | WordFeature.NestedDocuments)]
    public virtual async Task An_Invoice_Assembled_From_Nested_Templates_Is_Filled()
    {
        var input = TemplateInput(Path.Combine("Factuur", "invoice.dotx"));
        input.GlobalParameters = DictionaryUtility.Flatten(DictionaryUtility.ToDictionary(Invoice))!;
        input.CollectionParameters!.Add("invoiceLines", InvoiceLines());
        input.DocumentParameters = new[] { "header.dotx", "customer.dotx", "invoice-details.dotx", "invoice-lines.dotx", "invoice-summary.dotx" }
            .ToDictionary(name => name, name => TemplateInput(Path.Combine("Factuur", name)));
        input.DocumentParameters["footer.dotx"] = TemplateInput(Path.Combine("Factuur", "footer-inline.dotx"));

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain(Invoice.customer.title));
            Assert.That(facts.BodyText, Does.Contain("Overdracht gegevens - opstellen en bijwerken testomgeving"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    private static readonly Invoice Invoice = new(
        "Nieuwe server: installatie en configuratie", "2019.014", "000/0019/01402", "30-06-2019", "31-07-2019",
        new InvoiceCustomer("Ambulancecentrum Antwerpen BVBA", "Heiligstraat 139", "B-2620 Hemiksem"),
        "1.572,50", "1.902,73", "330,23");

    private static ICollection<IDictionary<string, object>> InvoiceLines() =>
    [
        DictionaryUtility.ToDictionary(new { quantity = "1", unitCategory = "st.", invoiceLineTitle = "Installatie besturingssystemen: inbegrepen in prijs van server", pricePerUnit = "0", taxTariff = "21,00", priceExcl = "0,00", tax = "0,00" })!,
        DictionaryUtility.ToDictionary(new { quantity = "11", unitCategory = "u.", invoiceLineTitle = "Overdracht gegevens - opstellen en bijwerken testomgeving", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "935,00", tax = "196,35" })!,
        DictionaryUtility.ToDictionary(new { quantity = "4,5", unitCategory = "u.", invoiceLineTitle = "Effectieve overdracht gegevens en controle", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "382,5", tax = "80,33" })!,
        DictionaryUtility.ToDictionary(new { quantity = "3", unitCategory = "u.", invoiceLineTitle = "Opstellen en testen backupschema's voor de verschillende servers en virtuele computers", pricePerUnit = "85,00", taxTariff = "21,00", priceExcl = "255,00", tax = "53,55" })!
    ];


    // ---- conditional blocks ----

    [TestCase(true)]
    [TestCase(false)]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Conditional_Block_Keeps_The_Branch_That_Holds(bool isPaid)
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document("Intro", "{{#if IsPaid}}", "Thank you, {{Customer}}.", "{{else}}", "Please pay, {{Customer}}.", "{{/if}}", "Outro"),
            GlobalParameters = new Dictionary<string, object> { ["IsPaid"] = isPaid, ["Customer"] = "Alice" }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(text, isPaid ? Does.Contain("Thank you, Alice.") : Does.Contain("Please pay, Alice."));
            Assert.That(text, isPaid ? Does.Not.Contain("Please pay") : Does.Not.Contain("Thank you"));
            Assert.That(text, Does.Contain("Intro").And.Contain("Outro"));
            Assert.That(text, Does.Not.Contain("{{"), "the marker paragraphs go with the dropped branch");
        });
    }

    /// <summary>
    /// Blocks are resolved before any parameter is written, so a value holding marker text is written as text: it opens
    /// no block, and fails none.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Parameter_Value_Holding_A_Marker_Is_Written_As_Text()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document("{{#if IsPaid}}", "PAIDBRANCH", "{{/if}}", "{{Note}}", "Outro"),
            GlobalParameters = new Dictionary<string, object> { ["IsPaid"] = false, ["Note"] = "{{#if IsPaid}}" }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("{{#if IsPaid}}"), "the value, as written");
            Assert.That(text, Does.Contain("Outro"));
            Assert.That(text, Does.Not.Contain("PAIDBRANCH"), "the block itself is resolved");
        });
    }

    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Condition_Is_False_For_A_Missing_Key_And_An_Empty_Value()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document(
                "{{#if Missing}}", "MISSINGKEY", "{{/if}}",
                "{{#if Blank}}", "BLANKVALUE", "{{/if}}",
                "{{#if Zero}}", "ZEROVALUE", "{{/if}}",
                "{{#if Off}}", "FALSEVALUE", "{{/if}}",
                "{{#if NoRows}}", "EMPTYCOLLECTION", "{{/if}}",
                "{{#if text}}", "TEXTVALUE", "{{/if}}",
                "{{#if One}}", "NUMBERVALUE", "{{/if}}",
                "{{#if Rows}}", "ROWSVALUE", "{{/if}}",
                "{{#if !Missing}}", "NEGATEDVALUE", "{{/if}}"),
            GlobalParameters = new Dictionary<string, object> { ["Blank"] = " ", ["Zero"] = 0, ["Off"] = false, ["Text"] = "yes", ["One"] = 1 },
            CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
            {
                ["NoRows"] = [],
                ["Rows"] = TemplateRows()
            }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            foreach (var dropped in new[] { "MISSINGKEY", "BLANKVALUE", "ZEROVALUE", "FALSEVALUE", "EMPTYCOLLECTION" })
            {
                Assert.That(text, Does.Not.Contain(dropped));
            }
            foreach (var kept in new[] { "TEXTVALUE", "NUMBERVALUE", "ROWSVALUE", "NEGATEDVALUE" })
            {
                Assert.That(text, Does.Contain(kept));
            }
        });
    }

    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task Conditional_Blocks_Nest()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document(
                "{{#if Outer}}",
                "OUTERYES",
                "{{#if Inner}}", "INNERYES", "{{else}}", "INNERNO", "{{/if}}",
                "{{else}}",
                "OUTERNO",
                "{{#if !Inner}}", "HIDDENINNER", "{{/if}}",
                "{{/if}}"),
            GlobalParameters = new Dictionary<string, object> { ["Outer"] = true, ["Inner"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("OUTERYES").And.Contain("INNERNO"));
            Assert.That(text, Does.Not.Contain("INNERYES").And.Not.Contain("OUTERNO").And.Not.Contain("HIDDENINNER").And.Not.Contain("{{"));
        });
    }

    /// <summary>
    /// A block around a table drops the table; a block inside a cell drops the cell's paragraphs and leaves the
    /// cell with the paragraph Word requires.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Conditional_Block_Drops_A_Table_Or_A_Cells_Content()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("{{#if ShowTable}}"),
                Docx.Table(["TABLECELL"]),
                Docx.Paragraph("{{/if}}"),
                Docx.Table(["{{#if ShowNote}}", "CELLNOTE", "{{/if}}"], ["KEPTCELL"])
            ]),
            GlobalParameters = new Dictionary<string, object> { ["ShowTable"] = false, ["ShowNote"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("KEPTCELL"));
            Assert.That(facts.BodyText, Does.Not.Contain("TABLECELL").And.Not.Contain("CELLNOTE").And.Not.Contain("{{"));
            Assert.That(facts.TableRows, Has.Count.EqualTo(1));
            Assert.That(Docx.CellsEndWithParagraphs(output), Is.True);
        });
    }

    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Conditional_Block_In_A_Header_Is_Resolved()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document(Docx.Paragraphs("Body"), Docx.Paragraphs("{{#if Draft}}", "DRAFTMARK", "{{else}}", "FINALMARK", "{{/if}}")),
            GlobalParameters = new Dictionary<string, object> { ["Draft"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var header = string.Join("\n", (await ReadDocx(output)).Headers.Select(story => story.Text));

        Assert.Multiple(() =>
        {
            Assert.That(header, Does.Contain("FINALMARK"));
            Assert.That(header, Does.Not.Contain("DRAFTMARK").And.Not.Contain("{{"));
        });
    }

    /// <summary>
    /// A block is resolved before nested documents are inserted, so a dropped branch's <c>&lt;{ key }&gt;</c> is
    /// never filled.
    /// </summary>
    [TestCase(true)]
    [TestCase(false)]
    [Needs(WordFeature.Creating | WordFeature.NestedDocuments)]
    public virtual async Task A_Dropped_Branch_Inserts_No_Nested_Document(bool hasAppendix)
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document("MAINTEXT", "{{#if HasAppendix}}", "<{ Appendix }>", "{{/if}}"),
            GlobalParameters = new Dictionary<string, object> { ["HasAppendix"] = hasAppendix },
            DocumentParameters = new Dictionary<string, WordTemplateInput>
            {
                ["Appendix"] = new() { Template = Docx.Document("APPENDIXTEXT") }
            }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("MAINTEXT"));
            Assert.That(facts.BodyText, hasAppendix ? Does.Contain("APPENDIXTEXT") : Does.Not.Contain("APPENDIXTEXT"));
            Assert.That(facts.Leftovers, Is.Empty);
        });
    }

    /// <summary>
    /// A marker counts in a paragraph's visible text only: one in a deleted revision or a field code is not a block,
    /// on every backend, as the Gotenberg check reads it.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task Markers_In_Deleted_Revisions_And_Field_Codes_Do_Not_Count()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("Intro"),
                Docx.DeletedParagraph("{{#if IsDraft}}"),
                Docx.Paragraph("DRAFTTEXT"),
                Docx.FieldParagraph(" QUOTE \"{{/if}}\" ", "QUOTED"),
                Docx.Paragraph("Outro")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.That(text, Does.Contain("Intro").And.Contain("DRAFTTEXT").And.Contain("Outro"));
    }

    /// <summary>
    /// A key edited under track changes reads as edited: <c>{{#if IsPaid}}</c> changed to <c>{{#if IsSettled}}</c>
    /// tests <c>IsSettled</c>.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Marker_Edited_Under_Track_Changes_Reads_As_Edited()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.EditedParagraph("{{#if ", "IsPaid", "IsSettled", "}}"),
                Docx.Paragraph("PAIDTEXT"),
                Docx.Paragraph("{{/if}}"),
                Docx.Paragraph("Outro")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsSettled"] = true }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("PAIDTEXT").And.Contain("Outro"));
            Assert.That(text, Does.Not.Contain("{{"));
        });
    }

    /// <summary>
    /// A document where no paragraph opens a block uses none, so a finished document that writes about templates
    /// creates and reads as it is: marker text among other text, and another template language's tags on lines of
    /// their own — a bare <c>{{else}}</c> included.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.TextExtraction)]
    public virtual async Task Marker_Text_In_A_Document_Without_Blocks_Stays_As_It_Is()
    {
        var documents = new (IMemoryFile Template, string[] Kept)[]
        {
            (Docx.Document("Intro", "Wrap optional text in {{#if Key}} and {{/if}}."),
                ["Wrap optional text in {{#if Key}} and {{/if}}."]),
            (Docx.Document("A Handlebars sample:", "{{#each items}}", "{{name}}", "{{else}}", "No items.", "{{/each}}"),
                ["{{#each items}}", "{{else}}", "No items.", "{{/each}}"]),
            (Docx.Document("A block closes with", "{{/if}}"),
                ["{{/if}}"])
        };

        for (var i = 0; i < documents.Length; i++)
        {
            var (template, kept) = documents[i];
            using var output = await RequireCreator().Create(new WordTemplateInput { Template = template });
            var created = (await ReadDocx(output, $"-{i + 1}")).BodyText;
            var read = await RequireTextExtractor().GetText(new WordTemplateInput { Template = template });

            Assert.Multiple(() =>
            {
                foreach (var text in kept)
                {
                    Assert.That(created, Does.Contain(text));
                    Assert.That(read, Does.Contain(text));
                }
            });
        }
    }

    /// <summary>
    /// A dropped branch that was all a text box or a content control held leaves the paragraph Word requires there.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Conditional_Block_In_A_Text_Box_Or_Content_Control_Leaves_A_Paragraph()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("KEPTTEXT"),
                Docx.TextBox("{{#if IsPaid}}", "PAIDSTAMP", "{{/if}}"),
                Docx.ContentControl("{{#if IsPaid}}", "PAIDNOTE", "{{/if}}"),
                Docx.Paragraph("Outro")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsPaid"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("KEPTTEXT").And.Contain("Outro"));
            Assert.That(text, Does.Not.Contain("PAIDSTAMP").And.Not.Contain("PAIDNOTE").And.Not.Contain("{{"));
            Assert.That(Docx.Count<W.TextBoxContent>(output), Is.GreaterThan(0), "the text box stays");
            Assert.That(Docx.TextBodiesEndWithParagraphs(output), Is.True);
        });
    }

    /// <summary>
    /// A tracked deletion inside the braces of a marker or a placeholder stays deleted: the visible text decides, a marker
    /// made of it resolves, and a document without blocks keeps its text as it is.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Deletion_Inside_The_Braces_Stays_Deleted()
    {
        var creator = RequireCreator();
        using var withBlock = await creator.Create(new WordTemplateInput
        {
            Template = Docx.Document([Docx.WithDeletion("{", "{", "{#if On}}"), Docx.Paragraph("KEEP"), Docx.Paragraph("{{/if}}"), Docx.Paragraph("after")]),
            GlobalParameters = new Dictionary<string, object> { ["On"] = true }
        });
        using var withoutBlock = await creator.Create(new WordTemplateInput
        {
            Template = Docx.Document([Docx.WithDeletion("Hello {", "{", "{Name}} there")])
        });
        var withBlockText = (await ReadDocx(withBlock, "-with-block")).BodyText;
        var withoutBlockText = (await ReadDocx(withoutBlock, "-without-block")).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(withBlockText, Does.Contain("KEEP").And.Contain("after").And.Not.Contain("{{"));
            Assert.That(withoutBlockText, Does.Contain("Hello {{Name}} there").And.Not.Contain("{{{"));
        });
    }

    /// <summary>
    /// A block in one of a group's text boxes resolves as one in a lone text box does, and is found when it is the
    /// template's only block; the group's other boxes stay.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Conditional_Block_In_A_Grouped_Text_Box_Is_Resolved()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("KEPTTEXT"),
                Docx.GroupedTextBoxes(["{{#if IsDraft}}", "DRAFTSTAMP", "{{/if}}"], ["OTHERBOX"]),
                Docx.Paragraph("Outro")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("KEPTTEXT").And.Contain("OTHERBOX").And.Contain("Outro"));
            Assert.That(text, Does.Not.Contain("DRAFTSTAMP").And.Not.Contain("{{"));
        });
    }

    /// <summary>
    /// The markers in a group's text boxes are read in any case, as in the body: an else written in capitals splits the
    /// block there.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Conditional_Block_In_A_Grouped_Text_Box_Reads_Its_Markers_In_Any_Case()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("KEPTTEXT"),
                Docx.GroupedTextBoxes(["{{#If IsDraft}}", "DRAFTSTAMP", "{{ELSE}}", "FINALSTAMP", "{{/IF}}"], ["OTHERBOX"])
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = true }
        };

        using var output = await RequireCreator().Create(input);
        var text = (await ReadDocx(output)).BodyText;

        Assert.Multiple(() =>
        {
            Assert.That(text, Does.Contain("KEPTTEXT").And.Contain("OTHERBOX").And.Contain("DRAFTSTAMP"));
            Assert.That(text, Does.Not.Contain("FINALSTAMP").And.Not.Contain("{{"));
        });
    }

    /// <summary>
    /// A group of text boxes in a header holds blocks as one in the body does, and its block is found when it is the
    /// template's only one.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Conditional_Block_In_A_Grouped_Text_Box_In_A_Header_Is_Resolved()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document(
                [Docx.Paragraph("BODYTEXT")],
                header: [Docx.GroupedTextBoxes(["{{#if IsDraft}}", "DRAFTSTAMP", "{{/if}}"], ["OTHERBOX"])]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var header = string.Join("\n", (await ReadDocx(output)).Headers.Select(story => story.Text));

        Assert.Multiple(() =>
        {
            Assert.That(header, Does.Contain("OTHERBOX"));
            Assert.That(header, Does.Not.Contain("DRAFTSTAMP").And.Not.Contain("{{"));
        });
    }

    /// <summary>
    /// A footnote, endnote or comment is not part of a template's blocks: a block in a footnote stays as text beside
    /// one the body resolves.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Block_In_A_Footnote_Stays_As_Text()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document(
                [Docx.FootnoteReference("Intro"), Docx.Paragraph("{{#if IsDraft}}"), Docx.Paragraph("DRAFTTEXT"), Docx.Paragraph("{{/if}}")],
                footnote: Docx.Paragraphs("{{#if IsDraft}}", "FOOTNOTETEXT", "{{/if}}")),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("Intro").And.Not.Contain("DRAFTTEXT"));
            Assert.That(Docx.FootnoteText(output), Does.Contain("{{#if IsDraft}}").And.Contain("FOOTNOTETEXT"));
        });
    }

    /// <summary>
    /// A marker paragraph that also ends a section goes, and the section break stays without the paragraph's own
    /// formatting.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Marker_Paragraph_Ending_A_Section_Leaves_Only_The_Break()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([
                Docx.Paragraph("{{#if IsDraft}}"),
                Docx.Paragraph("DRAFTTEXT"),
                Docx.SectionBreak("{{/if}}", pageBreakBefore: true),
                Docx.Paragraph("NEXTSECTION")
            ]),
            GlobalParameters = new Dictionary<string, object> { ["IsDraft"] = false }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("NEXTSECTION"));
            Assert.That(facts.BodyText, Does.Not.Contain("DRAFTTEXT").And.Not.Contain("{{"));
            Assert.That(facts.Sections, Has.Count.EqualTo(2), "the section break stays");
            Assert.That(Docx.Count<W.PageBreakBefore>(output), Is.Zero);
        });
    }

    /// <summary>
    /// Every case opens a block somewhere, so the document uses blocks and its markers are checked.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Malformed_Conditional_Block_Fails()
    {
        var creator = RequireCreator();
        async Task<FormatException> Fails(IMemoryFile template)
            => (await Assert.ThrowsAsync<FormatException>(() => creator.Create(new WordTemplateInput { Template = template })))!;

        var unclosed = await Fails(Docx.Document("{{#if IsPaid}}", "Thank you."));
        var unopened = await Fails(Docx.Document("{{#if IsPaid}}", "Paid.", "{{/if}}", "Thank you.", "{{/if}}"));
        var twoElses = await Fails(Docx.Document("{{#if IsPaid}}", "{{else}}", "{{else}}", "{{/if}}"));
        var amongText = await Fails(Docx.Document("Dear {{#if IsCompany}}Sir or Madam{{/if}},", "{{#if IsPaid}}", "Thank you.", "{{/if}}"));
        var unknown = await Fails(Docx.Document("{{#if IsDue}}", "{{#unless IsPaid}}", "Please pay.", "{{/unless}}", "{{/if}}"));
        var acrossSections = await Fails(Docx.Document([Docx.Paragraph("{{#if IsPaid}}"), Docx.SectionBreak("Thank you."), Docx.Paragraph("{{/if}}")]));
        // a content control around whole paragraphs is a container of its own
        var intoContentControl = await Fails(Docx.Document([Docx.Paragraph("{{#if IsPaid}}"), Docx.ContentControl("Thank you.", "{{/if}}")]));

        Assert.Multiple(() =>
        {
            Assert.That(unclosed.Message, Does.Contain("{{#if IsPaid}}").And.Contain("{{/if}}"));
            Assert.That(unopened.Message, Does.Contain("{{/if}}"));
            Assert.That(twoElses.Message, Does.Contain("{{else}}"));
            Assert.That(amongText.Message, Does.Contain("stands alone"));
            Assert.That(unknown.Message, Does.Contain("{{#unless IsPaid}}").And.Contain("not a conditional marker"));
            Assert.That(acrossSections.Message, Does.Contain("{{#if IsPaid}}").And.Contain("section"));
            Assert.That(intoContentControl.Message, Does.Contain("content control"));
        });
    }


    // ---- text boxes ----

    /// <summary>
    /// Word writes a text box twice: DrawingML for Word 2010 and later, and a VML fallback for readers that do not know
    /// DrawingML shapes, plain Open XML text extraction among them. A parameter and a block in the box are filled and
    /// resolved in both copies, in the body and in a header, so no reader meets a tag, whichever copy it reads.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating)]
    public virtual async Task A_Text_Box_Is_Filled_In_Both_Of_Its_Copies()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document(
                [
                    Docx.Paragraph("Intro"),
                    Docx.TextBoxWithFallback("{{ Customer }}", "{{#if IsPaid}}", "PAIDSTAMP", "{{else}}", "Please pay, {{ Customer }}.", "{{/if}}")
                ],
                header: [Docx.TextBoxWithFallback("{{ Title }}")]),
            GlobalParameters = new Dictionary<string, object> { ["Customer"] = "Alice", ["IsPaid"] = false, ["Title"] = "INVOICETITLE" }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);
        var copies = Docx.TextBoxCopies(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Leftovers, Is.Empty);
            Assert.That(facts.BodyText, Does.Contain("Please pay, Alice.").And.Not.Contain("PAIDSTAMP"));
            Assert.That(facts.Headers.Select(story => story.Text), Has.Some.Contains("INVOICETITLE"));
            Assert.That(copies.Select(copy => copy.Fallback), Has.Some.Contains("Please pay, Alice.").And.Some.Contains("INVOICETITLE"),
                "the fallback copies are filled");
            Assert.That(copies, Has.All.Matches<(string Choice, string Fallback)>(copy => copy.Fallback == copy.Choice),
                "the fallback copy reads as the DrawingML copy");
        });
    }

    /// <summary>
    /// A text box keeps both of its copies filled through a merge, which saves each input and reads it again on the way.
    /// </summary>
    [Test]
    [Needs(WordFeature.Merging)]
    public virtual async Task A_Text_Box_Is_Filled_In_Both_Of_Its_Copies_Through_A_Merge()
    {
        var inputs = new WordTemplateInput[]
        {
            new() { Template = Docx.Document("ALPHA") },
            new()
            {
                Template = Docx.Document([Docx.TextBoxWithFallback("{{ Customer }}")]),
                GlobalParameters = new Dictionary<string, object> { ["Customer"] = "Alice" }
            }
        };

        using var merged = await RequireMerger().Merge(inputs);
        var facts = await ReadDocx(merged);
        var copies = Docx.TextBoxCopies(merged);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Leftovers, Is.Empty);
            Assert.That(copies.Select(copy => copy.Fallback), Has.Some.Contains("Alice"), "the fallback copy is filled");
            Assert.That(copies, Has.All.Matches<(string Choice, string Fallback)>(copy => copy.Fallback == copy.Choice),
                "the fallback copy reads as the DrawingML copy");
        });
    }

    /// <summary>
    /// A nested document's <c>&lt;{ key }&gt;</c> in a text box is replaced in both of the box's copies, as a parameter is.
    /// </summary>
    [Test]
    [Needs(WordFeature.Creating | WordFeature.NestedDocuments)]
    public virtual async Task A_Nested_Document_In_A_Text_Box_Is_Inserted_In_Both_Of_Its_Copies()
    {
        var input = new WordTemplateInput
        {
            Template = Docx.Document([Docx.Paragraph("MAINTEXT"), Docx.TextBoxWithFallback("<{ Appendix }>")]),
            DocumentParameters = new Dictionary<string, WordTemplateInput>
            {
                ["Appendix"] = new() { Template = Docx.Document("APPENDIXTEXT") }
            }
        };

        using var output = await RequireCreator().Create(input);
        var facts = await ReadDocx(output);
        var copies = Docx.TextBoxCopies(output);

        Assert.Multiple(() =>
        {
            Assert.That(facts.Leftovers, Is.Empty);
            Assert.That(facts.BodyText, Does.Contain("MAINTEXT").And.Contain("APPENDIXTEXT"));
            Assert.That(copies.Select(copy => copy.Fallback), Has.Some.Contains("APPENDIXTEXT"), "the fallback copy holds the nested document");
            Assert.That(copies, Has.All.Matches<(string Choice, string Fallback)>(copy => copy.Fallback == copy.Choice),
                "the fallback copy reads as the DrawingML copy");
        });
    }


    // ---- converting ----

    /// <summary>
    /// The output is in the format asked for, and its content type names that format, so a web app can serve
    /// <c>output.ContentType</c> as it is.
    /// </summary>
#pragma warning disable NUnit1011 // the member is the fixture's: each backend declares its own OutputFormats
    [TestCaseSource("OutputFormats")]
#pragma warning restore NUnit1011
    [Needs(WordFeature.Converting)]
    public virtual async Task Convert_To(FileFormat format, string contentType)
    {
        using var output = await RequireConverter().Convert(TemplateInput("template.docx"), format);
        await Save(output, Extension(format));

        Assert.Multiple(() =>
        {
            Assert.That(Facts.Sniff(output.GetBytes()!), Is.EqualTo(format));
            Assert.That(output.ContentType, Is.EqualTo(contentType));
        });
    }

    /// <summary>A3 pages stay A3 when converted without page settings.</summary>
    [Test]
    [Needs(WordFeature.Converting)]
    public virtual async Task From_A3_To_Pdf()
    {
        using var output = await RequireConverter().Convert(TemplateInput("template_a3.docx"), FileFormat.Pdf);
        var pdf = await ReadPdf(output);

        Assert.That(pdf.PageSizes, Is.All.EqualTo(Facts.A3).Using<(int Width, int Height)>(WithinTwoPoints));
    }

    [TestCase(PageSize.A3, PageOrientation.Portrait, 842, 1191)]
    [TestCase(PageSize.A4, PageOrientation.Landscape, 842, 595)]
    // sizes some vendors' own page-size lists do not name
    [TestCase(PageSize.A2, PageOrientation.Portrait, 1191, 1684)]
    [TestCase(PageSize.A6, PageOrientation.Portrait, 298, 420)]
    [TestCase(PageSize.A7, PageOrientation.Portrait, 210, 298)]
    [Needs(WordFeature.Converting)]
    public virtual async Task Page_Settings_Set_The_Rendered_Page_Size(PageSize size, PageOrientation orientation, int width, int height)
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = size, PageOrientation = orientation }
        };

        using var output = await RequireConverter().Convert(TemplateInput("template.docx"), options);
        var pdf = await ReadPdf(output);

        // the first page: the FreeSpire.Doc free edition ends a longer PDF with a notice page of its own
        Assert.Multiple(() =>
        {
            Assert.That(pdf.PageSizes[0].Width, Is.EqualTo(width).Within(2));
            Assert.That(pdf.PageSizes[0].Height, Is.EqualTo(height).Within(2));
        });
    }

    /// <summary>
    /// Page settings apply to a landscape section as to a portrait one: the size asked for, turned the way asked for.
    /// </summary>
    [TestCase(PageSize.A3, PageOrientation.Landscape, 1191, 842)]
    [TestCase(PageSize.A3, PageOrientation.Portrait, 842, 1191)]
    [TestCase(PageSize.A5, PageOrientation.Landscape, 595, 420)]
    [Needs(WordFeature.Converting)]
    public virtual async Task Page_Settings_Apply_To_A_Landscape_Section(PageSize size, PageOrientation orientation, int width, int height)
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = size, PageOrientation = orientation }
        };

        using var output = await RequireConverter().Convert(new WordTemplateInput { Template = Docx.Document(Docx.Paragraphs("A landscape page"), landscape: true) }, options);
        var pdf = await ReadPdf(output);

        Assert.Multiple(() =>
        {
            Assert.That(pdf.PageSizes[0].Width, Is.EqualTo(width).Within(2));
            Assert.That(pdf.PageSizes[0].Height, Is.EqualTo(height).Within(2));
        });
    }

    /// <summary>
    /// The document written from a landscape section holds the size asked for, turned the way asked for: the page Word
    /// lays out, and the text width pictures and tables scale with.
    /// </summary>
    [TestCase(PageSize.A3, PageOrientation.Landscape, 1191, 842)]
    [TestCase(PageSize.A3, PageOrientation.Portrait, 842, 1191)]
    [TestCase(PageSize.A5, PageOrientation.Landscape, 595, 420)]
    [Needs(WordFeature.Converting | WordFeature.OtherFormats)]
    public virtual async Task Page_Settings_Write_A_Landscape_Section_In_The_Size_Asked_For(PageSize size, PageOrientation orientation, int width, int height)
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Docx,
            Settings = new DocumentSettings { PageSize = size, PageOrientation = orientation }
        };

        using var output = await RequireConverter().Convert(new WordTemplateInput { Template = Docx.Document(Docx.Paragraphs("A landscape page"), landscape: true) }, options);
        var section = (await ReadDocx(output)).Sections[0];

        // twips
        Assert.Multiple(() =>
        {
            Assert.That(section.PageWidth / 20d, Is.EqualTo(width).Within(2));
            Assert.That(section.PageHeight / 20d, Is.EqualTo(height).Within(2));
        });
    }

    /// <summary>
    /// A larger page scales a picture with the text width (<see cref="ConversionOptions.AutoScalePictures"/>): by as much
    /// as the text width grows, keeping its proportions.
    /// </summary>
    [Test]
    [Needs(WordFeature.Converting | WordFeature.OtherFormats)]
    public virtual async Task AutoScalePictures_Scales_A_Picture_With_The_Text_Width()
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Docx,
            Settings = new DocumentSettings { PageSize = PageSize.A3 }
        };

        using var output = await RequireConverter().Convert(TemplateInput("template.docx"), options);
        var converted = await ReadDocx(output);
        var original = DocxFacts.Read(ReadAsset("template.docx"));
        var textWidthRatio = (double)converted.Sections[0].TextWidth!.Value / original.Sections[0].TextWidth!.Value;

        static double Proportion(PictureFact picture) => (double)picture.ExtentHeight!.Value / picture.ExtentWidth!.Value;
        Assert.That(converted.Pictures, Has.Count.EqualTo(original.Pictures.Count));
        Assert.Multiple(() =>
        {
            Assert.That(textWidthRatio, Is.GreaterThan(1.3), "A3 text is wider than A4 text");
            foreach (var (before, after) in original.Pictures.Zip(converted.Pictures))
            {
                Assert.That((double)after.ExtentWidth!.Value / before.ExtentWidth!.Value, Is.EqualTo(textWidthRatio).Within(2).Percent, "scaled with the text width");
                Assert.That(Proportion(after), Is.EqualTo(Proportion(before)).Within(1).Percent, "height over width");
            }
        });
    }

    /// <summary>
    /// A picture scaled with the text width stops at the largest shape Word holds, 22 inches, and keeps its proportions:
    /// on A0, the text is five times as wide as on A4.
    /// </summary>
    [Test]
    [Needs(WordFeature.Converting | WordFeature.OtherFormats)]
    public virtual async Task AutoScalePictures_Stops_At_The_Largest_Shape_Word_Holds()
    {
        const long maxShapeSize = 1584 * 12700; // 22 inches in EMUs
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Docx,
            Settings = new DocumentSettings { PageSize = PageSize.A0 }
        };

        using var output = await RequireConverter().Convert(TemplateInput("template.docx"), options);
        var converted = await ReadDocx(output);
        var original = DocxFacts.Read(ReadAsset("template.docx"));

        static double Proportion(PictureFact picture) => (double)picture.ExtentHeight!.Value / picture.ExtentWidth!.Value;
        Assert.That(converted.Pictures, Has.Count.EqualTo(original.Pictures.Count));
        Assert.Multiple(() =>
        {
            foreach (var (before, after) in original.Pictures.Zip(converted.Pictures))
            {
                Assert.That(Math.Max(after.ExtentWidth!.Value, after.ExtentHeight!.Value), Is.LessThanOrEqualTo(maxShapeSize).Within(0.5).Percent, "at most 22 inches");
                Assert.That(after.ExtentWidth, Is.GreaterThan(before.ExtentWidth), "still scaled up");
                Assert.That(Proportion(after), Is.EqualTo(Proportion(before)).Within(1).Percent, "height over width");
            }
        });
    }

    /// <summary>
    /// A picture taller than wide stops at the limit by its height, keeping its proportions. Its aspect ratio is locked,
    /// as Word locks it, and a locked picture in Spire.Doc recalculates one side from the other as each is set.
    /// </summary>
    [TestCase(400.0)]
    [TestCase(437.3)]
    [TestCase(468.9)]
    [TestCase(512.2)]
    [TestCase(555.5)]
    [TestCase(601.7)]
    [Needs(WordFeature.Converting | WordFeature.OtherFormats)]
    public virtual async Task AutoScalePictures_Stops_A_Tall_Picture_At_The_Limit(double height)
    {
        var input = Docx.WithPictureSize(ReadAsset("template_image.docx"), height * 0.7, height);
        var original = DocxFacts.Read(input).Pictures.Single();
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Docx,
            Settings = new DocumentSettings { PageSize = PageSize.A0 }
        };

        using var output = await RequireConverter().Convert(new WordTemplateInput { Template = input }, options);
        var picture = (await ReadDocx(output)).Pictures.Single(picture => picture.Sha1 == original.Sha1);

        Assert.Multiple(() =>
        {
            Assert.That(picture.ExtentHeight / 12700d, Is.EqualTo(1584).Within(0.01), "points");
            Assert.That((double)picture.ExtentHeight!.Value / picture.ExtentWidth!.Value,
                Is.EqualTo((double)original.ExtentHeight!.Value / original.ExtentWidth!.Value).Within(1).Percent, "height over width");
        });
    }

    /// <summary>
    /// A picture already past the 22-inch limit — DocIO writes one — is left as it is by page settings that keep the text
    /// width, and brought to the limit by settings that change it, whichever way, unless the text width takes it below
    /// that. <c>template_image.docx</c> is A4, with 70.85 pt side margins.
    /// </summary>
    [TestCase(PageSize.A4, null, 2000, TestName = "{m}(the text width kept)")]
    [TestCase(PageSize.A4, 65f, 1584, TestName = "{m}(the text width 3 percent wider)")]
    [TestCase(PageSize.A4, 76f, 1584, TestName = "{m}(the text width 2 percent narrower)")]
    [TestCase(PageSize.A3, null, 1584, TestName = "{m}(A3)")]
    [TestCase(PageSize.A5, null, 1225, TestName = "{m}(A5)")]
    [Needs(WordFeature.Converting | WordFeature.OtherFormats)]
    public virtual async Task AutoScalePictures_Brings_A_Picture_Past_The_Limit_To_It_When_The_Text_Width_Changes(PageSize size, float? margins, int width)
    {
        var input = Docx.WithPictureWidth(ReadAsset("template_image.docx"), 2000);
        var original = DocxFacts.Read(input).Pictures.Single();
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Docx,
            Settings = new DocumentSettings { PageSize = size, Margins = margins is { } points ? (Margins)points : null }
        };

        using var output = await RequireConverter().Convert(new WordTemplateInput { Template = input }, options);
        var picture = (await ReadDocx(output)).Pictures.Single(picture => picture.Sha1 == original.Sha1);

        Assert.Multiple(() =>
        {
            Assert.That(picture.ExtentWidth / 12700d, Is.EqualTo(width).Within(2), "points");
            Assert.That((double)picture.ExtentHeight!.Value / picture.ExtentWidth!.Value,
                Is.EqualTo((double)original.ExtentHeight!.Value / original.ExtentWidth!.Value).Within(1).Percent, "height over width");
        });
    }

    /// <summary>Saving HTML to a stream has nowhere to put an images folder or a stylesheet.</summary>
    [Test]
    [Needs(WordFeature.Converting | WordFeature.OtherFormats)]
    public virtual async Task Convert_To_Html_Is_Self_Contained()
    {
        using var output = await RequireConverter().Convert(TemplateInput("template.docx"), FileFormat.Html);
        await Save(output, "html");
        var html = System.Text.Encoding.UTF8.GetString(output.GetBytes()!);

        Assert.Multiple(() =>
        {
            Assert.That(html, Does.Contain("<img"));
            Assert.That(html, Does.Contain("src=\"data:image"));
            Assert.That(html, Does.Not.Contain(".png\""));
            Assert.That(html, Does.Not.Contain(".css\""));
        });
    }

    [TestCase(FileFormat.Png)]
    [TestCase(FileFormat.Jpeg)]
    [Needs(WordFeature.Converting)]
    public virtual async Task Convert_To_Image_Points_At_ToImages(FileFormat format)
    {
        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => RequireConverter().Convert(TemplateInput("template.docx"), format));

        Assert.That(ex!.Message, Does.Contain("ToImages"));
    }


    // ---- page images ----

    /// <summary>One image per page of <c>template.docx</c>: two A4-shaped pages with something on them.</summary>
    [Test]
    [Needs(WordFeature.PageImages)]
    public virtual async Task To_Images()
    {
        var images = (await RequireToImages().ToImages(TemplateInput("template.docx"))).ToList();
        var facts = new List<ImageFacts>();
        for (var i = 0; i < images.Count; i++)
        {
            facts.Add(await ReadImage(images[i], $"-{i + 1}"));
            images[i].Dispose();
        }

        Assert.That(facts, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(facts.Select(image => (double)image.Height / image.Width), Is.All.EqualTo(Math.Sqrt(2)).Within(0.01), "A4: height over width is √2");
            Assert.That(facts.Select(image => image.Ink), Is.All.GreaterThan(0.05), "a rendered page, not a blank one");
        });
    }

    [Test]
    [Needs(WordFeature.PageImages | WordFeature.Converting)]
    public virtual async Task ToImages_Returns_One_Image_Per_Page()
    {
        using var pdf = await RequireConverter().Convert(TemplateInput("multipage.docx"), FileFormat.Pdf);
        var pages = await ReadPdf(pdf);
        var images = (await RequireToImages().ToImages(TemplateInput("multipage.docx"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(pages.Pages, Is.GreaterThan(1));
            Assert.That(images, Has.Count.EqualTo(pages.Pages));
        });
        images.ForEach(image => image.Dispose());
    }


    // ---- extracting ----

    [Test]
    [Needs(WordFeature.ImageExtraction)]
    public virtual async Task GetImages()
    {
        var images = (await RequireImageExtractor().GetImages(TemplateInput("template.docx"))).ToList();
        var template = DocxFacts.Read(ReadAsset("template.docx"));

        Assert.That(images.Select(image => PictureFact.Of(image.File!.GetBytes()!).Sha1), Is.SupersetOf(template.Pictures.Select(picture => picture.Sha1)),
            "the template's pictures, byte for byte");
    }

    [Test]
    [Needs(WordFeature.TextExtraction)]
    public virtual async Task GetText()
    {
        var text = await RequireTextExtractor().GetText(TemplateInput("lorem_ipsum.docx"));

        Assert.That(text, Does.Contain("Lorem ipsum"));
    }


    // ---- document builder ----

    [Test]
    [Needs(WordFeature.DocumentBuilder | WordFeature.Converting)]
    public virtual async Task DocumentBuilder_Builds_Paragraphs_And_Headers()
    {
        var paragraphs = new List<Paragraph>
        {
            new() { Text = "Lorem Ipsum", Style = ParagraphStyle.Heading1 },
            new() { Text = LoremIpsum.Paragraphs.First(), PageBreakAfter = true },
            new() { Text = "Second page", PageBreakAfter = true, Image = new WordImage { Name = "sample", File = ReadAsset("sample1.jpg"), Size = new(300, 169), HorizontalAlignment = HorizontalAlignment.Right } },
            new() { Text = "Third page" }
        };
        var headers = new[]
        {
            new WordHeaderFooterInput { Template = TemplateInput("firstpage_header.docx"), Type = HeaderFooterType.FirstPage },
            new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") }
        };

        using var docx = await Build(paragraphs, headers);
        var facts = await ReadDocx(docx);
        using var pdf = await RequireConverter().Convert(new WordTemplateInput { Template = docx }, FileFormat.Pdf);
        var pages = await ReadPdf(pdf);

        Assert.Multiple(() =>
        {
            Assert.That(facts.BodyText, Does.Contain("Lorem Ipsum").And.Contain("Third page"));
            Assert.That(facts.Pictures.Select(picture => picture.Sha1), Does.Contain(Asset("sample1.jpg").Sha1));
            Assert.That(facts.Headers.Select(header => header.Text), Has.Some.Contains("Header"));
            Assert.That(facts.Sections.Select(section => (section.PageWidth ?? 0, section.PageHeight ?? 0)), Is.All.EqualTo(Facts.A4Twips).Using<(int, int)>(WithinTwoPoints),
                "A4, like a blank document on every backend");
            // the two page breaks after a paragraph
            Assert.That(pages.Pages, Is.EqualTo(3));
        });
    }

    /// <summary>A builder joins the inputs it loads as <c>Merge</c> does, following on when asked.</summary>
    [TestCase(false)]
    [TestCase(true)]
    [Needs(WordFeature.DocumentBuilder)]
    public virtual async Task DocumentBuilder_Joins_Loaded_Inputs_As_Merge_Does(bool followOn)
    {
        using var docx = await Build([], [], inputs: MergeInputs(), merge: followOn ? new MergeOptions { FollowOn = true } : null);
        var facts = await ReadDocx(docx);

        Assert.That(facts.Sections.Skip(1).Select(section => section.Start),
            Is.EqualTo(followOn ? new[] { "continuous", "nextPage", "continuous" } : new[] { "nextPage", "nextPage", "nextPage" }));
    }

    /// <summary>A builder's page settings apply to a loaded landscape section as <c>Convert</c>'s do.</summary>
    [TestCase(PageOrientation.Landscape, 1191, 842)]
    [TestCase(PageOrientation.Portrait, 842, 1191)]
    [Needs(WordFeature.DocumentBuilder)]
    public virtual async Task DocumentBuilder_Settings_Apply_To_A_Loaded_Landscape_Section(PageOrientation orientation, int width, int height)
    {
        var inputs = new[] { new WordTemplateInput { Template = Docx.Document(Docx.Paragraphs("A landscape page"), landscape: true) } };

        using var docx = await Build([], [], inputs: inputs, settings: new DocumentSettings { PageSize = PageSize.A3, PageOrientation = orientation });
        var section = (await ReadDocx(docx)).Sections[0];

        // twips
        Assert.Multiple(() =>
        {
            Assert.That(section.PageWidth / 20d, Is.EqualTo(width).Within(2));
            Assert.That(section.PageHeight / 20d, Is.EqualTo(height).Within(2));
        });
    }

    /// <summary>A picture given a size takes that size, both ways, whatever the proportions of the image.</summary>
    [Test]
    [Needs(WordFeature.DocumentBuilder)]
    public virtual async Task DocumentBuilder_Sizes_A_Picture_As_Given()
    {
        // sample1.jpg is 1024 × 576; the size asked for is four times as wide as it is high
        var paragraphs = new[] { new Paragraph { Text = "A picture", Image = new WordImage { Name = "sample", File = ReadAsset("sample1.jpg"), Size = new(200, 50) } } };

        using var docx = await Build(paragraphs, []);
        var picture = (await ReadDocx(docx)).Pictures.Single(picture => picture.Sha1 == Asset("sample1.jpg").Sha1);

        Assert.That((double)picture.ExtentHeight!.Value / picture.ExtentWidth!.Value, Is.EqualTo(0.25).Within(1).Percent, "height over width");
    }

    // the build is typed by the format it was converted to, as Convert types it
    [TestCase(FileFormat.Pdf, "application/pdf")]
    [TestCase(FileFormat.Doc, "application/msword")]
    [TestCase(FileFormat.Docx, "application/vnd.openxmlformats-officedocument.wordprocessingml.document")]
    [Needs(WordFeature.DocumentBuilder)]
    public virtual async Task A_Build_Is_Typed_By_Its_Output_Format(FileFormat format, string contentType)
    {
        using var file = await Build([new Paragraph { Text = "Lorem Ipsum" }], [], new ConversionOptions { OutputFormat = format });
        await Save(file, Extension(format));

        Assert.Multiple(() =>
        {
            Assert.That(Facts.Sniff(file.GetBytes()!), Is.EqualTo(format));
            Assert.That(file.ContentType, Is.EqualTo(contentType));
        });
    }


    // ---- helpers ----

    private IWordConverter RequireConverter() => Require(Converter, WordFeature.Converting);
    private IWordMerger RequireMerger() => Require(Merger, WordFeature.Merging);
    private IWordCreator RequireCreator() => Require(Creator, WordFeature.Creating);
    private IWordToImagesService RequireToImages() => Require(ToImagesService, WordFeature.PageImages);
    private IWordImageExtractor RequireImageExtractor() => Require(ImageExtractor, WordFeature.ImageExtraction);
    private IWordTextExtractor RequireTextExtractor() => Require(TextExtractor, WordFeature.TextExtraction);

    /// <summary>
    /// The capability a scenario reached for. A backend without it fails the scenario: the fixture leaves the feature
    /// out, or the scenario's <see cref="NeedsAttribute"/> misses it.
    /// </summary>
    private TCapability Require<TCapability>(TCapability? capability, WordFeature feature)
        where TCapability : class
    {
        if (capability == null)
        {
            Assert.Fail($"{GetType().Name}'s backend is no {typeof(TCapability).Name}: the fixture leaves out {nameof(WordFeature)}.{feature}, or the scenario needs it.");
        }
        return capability!;
    }

    private PictureFact Asset(string filename) => PictureFact.Of(File.ReadAllBytes(InputPath(filename)));

    /// <summary>Where each text first occurs in <paramref name="text"/>; -1 for one that does not.</summary>
    private static int[] Order(string text, params string[] texts) => texts.Select(t => text.IndexOf(t, StringComparison.Ordinal)).ToArray();

    /// <summary>The first 40 characters of the reference's three longest paragraphs: enough to recognise its text.</summary>
    private static IEnumerable<string> Excerpts(DocxFacts reference)
        => reference.Paragraphs.Select(paragraph => paragraph.Text.Trim()).Where(text => text.Length >= 40).OrderByDescending(text => text.Length).Take(3).Select(text => text[..40]);

    private static string FirstLine(IEnumerable<StoryFact> stories)
        => stories.Select(story => story.Text.Split('\n')[0].Trim()).First(line => line.Length > 0);

    private static int MostCommon(IEnumerable<int> values) => values.GroupBy(value => value).OrderByDescending(group => group.Count()).First().Key;

    private static bool WithinTwoPoints((int Width, int Height) actual, (int Width, int Height) expected)
        => Math.Abs(actual.Width - expected.Width) <= 2 && Math.Abs(actual.Height - expected.Height) <= 2;

    private static ICollection<IDictionary<string, object>> TemplateRows() =>
    [
        DictionaryUtility.ToDictionary(new { Id = "10", Title = "Item #10", Price = 100 })!,
        DictionaryUtility.ToDictionary(new { Id = "12", Title = "Item #12", Price = 200 })!,
        DictionaryUtility.ToDictionary(new { Id = "31", Title = "Item #31", Price = 350 })!
    ];
}

// the invoice the realistic scenarios fill; lower-case members, as the template's tags spell them
internal sealed record Invoice(string invoiceTitle, string invoiceNumber, string ogmCode, string issueDate, string dueDate, InvoiceCustomer customer, string priceExclTotal, string priceInclTotal, string taxTotal);
internal sealed record InvoiceCustomer(string title, string street, string address);
