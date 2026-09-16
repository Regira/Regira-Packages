using NUnit.Framework.Legacy;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Models;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;
using Regira.Utilities;
using System.Globalization;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace Office.Word.testing.Abstractions;

/// <summary>
/// Shared scenarios for every backend that implements <see cref="IWordService"/>.
/// <para>
/// Bodies are <c>virtual</c> and carry no attributes: a derived fixture re-declares the ones it
/// supports with <c>[Test]</c>/<c>[TestCase]</c>, supplies its own cases, and overrides the body
/// outright where the backend behaves differently (an unsupported format, for example).
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

    /// <summary>
    /// Backends implement different subsets of <see cref="IWordService"/>, so the capabilities are
    /// probed rather than demanded: a scenario whose capability is absent ignores itself, the way
    /// <c>BarcodeTestsBase</c> handles a write-only or read-only barcode backend. Not even creating
    /// is common to all of them — a conversion-only backend implements none of the model side.
    /// </summary>
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

    /// <summary>
    /// Reads back the text of a produced document. Skips the calling test when the backend cannot
    /// extract text, since the assertion would have nothing to check.
    /// </summary>
    protected async Task<bool> HasContent(IMemoryFile file, string content)
    {
        if (TextExtractor == null)
        {
            Assert.Ignore("Text extraction not supported");
        }
        var text = await TextExtractor!.GetText(new WordTemplateInput { Template = file });
        return text.Contains(content);
    }

    private static async Task AssertSaved(IMemoryFile output, string outputPath)
    {
        var file = await output.SaveAs(outputPath);
        Assert.That(file.Exists, Is.True);
        ClassicAssert.Greater(file.Length, 0);
    }


    public virtual async Task From_File(string filename)
    {
        using var output = await RequireCreator().Create(TemplateInput(filename));
        await AssertSaved(output, OutputPath($"from_{Path.GetExtension(filename).TrimStart('.')}.docx"));
    }

    public virtual async Task Bookmarks()
    {
        var pi = Math.Round(Math.PI, 10).ToString(CultureInfo.InvariantCulture);
        var input = TemplateInput("bookmarks.dot");
        input.GlobalParameters = new Dictionary<string, object> { ["rs_Pi"] = pi };

        using var output = await RequireCreator().Create(input);
        await AssertSaved(output, OutputPath("bookmarks.docx"));

        Assert.That(await HasContent(output, pi), Is.True);
    }

    public virtual async Task Merge()
    {
        var mergedInputs = new[] { "doc-1.docx", "doc-2.docx" }
            .Select(name => new WordTemplateInput
            {
                Template = ReadAsset(name),
                Options = new InputOptions { InheritFont = true, EnforceEvenAmountOfPages = true }
            })
            .ToArray();

        using var merged = await RequireMerger().Merge(mergedInputs);

        var headerFooterInput = new WordTemplateInput { Template = merged.ToBinaryFile() };
        headerFooterInput.Headers!.Add(new WordHeaderFooterInput { Template = TemplateInput("header_footer.docx") });
        headerFooterInput.Footers!.Add(new WordHeaderFooterInput { Template = TemplateInput("header_footer.docx") });

        using var output = await RequireCreator().Create(headerFooterInput);
        await AssertSaved(output, OutputPath("merged.docx"));

        // content from both source documents must survive the merge
        Assert.That(await HasContent(output, "Vertical images"), Is.True);
        Assert.That(await HasContent(output, "Some text in Arial"), Is.True);
    }

    public virtual Task Add_Header_And_Footer() => AddHeaderAndFooter(HeaderFooterType.Default, "added_header_and_footer.docx");

    public virtual Task Add_FirstPage_Header_And_Footer() => AddHeaderAndFooter(HeaderFooterType.FirstPage, "added_firstpage_header_and_footer.docx");

    private async Task AddHeaderAndFooter(HeaderFooterType type, string outputName)
    {
        var input = TemplateInput("lorem_ipsum.docx");
        input.Headers!.Add(new WordHeaderFooterInput { Template = TemplateInput("add_header.docx"), Type = type });
        input.Footers!.Add(new WordHeaderFooterInput { Template = TemplateInput("add_footer.docx"), Type = type });

        using var output = await RequireCreator().Create(input);
        await AssertSaved(output, OutputPath(outputName));

        Assert.That(await HasContent(output, "Header"), Is.True);
        Assert.That(await HasContent(output, "Testing by"), Is.True);
        // the header/footer templates' body text must not leak into the document
        Assert.That(await HasContent(output, "Jibberisch"), Is.False);
    }

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
        await AssertSaved(output, OutputPath("parameters.docx"));

        foreach (var value in parameters.Values)
        {
            Assert.That(await HasContent(output, value.ToString()!), Is.True);
        }
        Assert.That(await HasContent(output, "{{ title }}"), Is.False);
    }

    public virtual async Task Replace_Image()
    {
        var input = TemplateInput("template_image.docx");
        input.Images!.Add(new WordImage
        {
            Name = "placeholder", // Alt Text
            File = ReadAsset("sample1.jpg")
        });

        using var output = await RequireCreator().Create(input);
        await AssertSaved(output, OutputPath("template_image.docx"));
    }

    public virtual async Task Template_Row()
    {
        var input = TemplateInput("template_row.docx");
        input.CollectionParameters!.Add("Template_Table", TemplateRows());

        using var output = await RequireCreator().Create(input);
        await AssertSaved(output, OutputPath("template_row.docx"));

        Assert.That(await HasContent(output, "Item #12"), Is.True);
        Assert.That(await HasContent(output, "{{ title }}"), Is.False);
    }

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
        await AssertSaved(output, OutputPath("nested_templates.docx"));

        Assert.That(await HasContent(output, "Item #12"), Is.True);
        Assert.That(await HasContent(output, "<{ doc2  }>"), Is.False);
    }

    /// <summary>
    /// A first-page or even-page version of one story switches those pages to stories of their own; the other story,
    /// given only a default version, must still appear on them — page numbers in a footer, say, under an even header.
    /// </summary>
    /// <param name="type"><see cref="HeaderFooterType.FirstPage"/> (page 1) or <see cref="HeaderFooterType.Even"/> (page 2)</param>
    /// <param name="specialHeader">Whether the header gets the special version, rather than the footer</param>
    public virtual async Task A_Story_Given_For_Some_Pages_Leaves_The_Other_Story_On_Them(HeaderFooterType type, bool specialHeader)
    {
        var input = new WordTemplateInput { Template = Document(("One", true), ("Two", false)) };
        var given = specialHeader ? input.Headers! : input.Footers!;
        var other = specialHeader ? input.Footers! : input.Headers!;
        given.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = Document(("GIVENDEFAULT", false)) } });
        given.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = Document(("GIVENSPECIAL", false)) }, Type = type });
        other.Add(new WordHeaderFooterInput { Template = new WordTemplateInput { Template = Document(("OTHERDEFAULT", false)) } });

        using var pdf = await RequireConverter().Convert(input, FileFormat.Pdf);
        var pages = PageTexts(pdf.GetBytes()!);
        var special = type == HeaderFooterType.FirstPage ? 0 : 1;
        var regular = 1 - special;

        Assert.That(pages, Has.Length.GreaterThanOrEqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(pages[special], Does.Contain("GIVENSPECIAL"));
            Assert.That(pages[special], Does.Contain("OTHERDEFAULT"), "the story given no special version must stay on the special page");
            Assert.That(pages[regular], Does.Contain("GIVENDEFAULT"));
            Assert.That(pages[regular], Does.Contain("OTHERDEFAULT"));
        });
    }

    /// <summary>A .docx of the given paragraphs, each optionally followed by a page break.</summary>
    private static IMemoryFile Document(params (string Text, bool PageBreakAfter)[] paragraphs)
    {
        using var stream = new MemoryStream();
        using (var doc = DocumentFormat.OpenXml.Packaging.WordprocessingDocument.Create(stream, DocumentFormat.OpenXml.WordprocessingDocumentType.Document))
        {
            var body = new W.Body();
            foreach (var (text, pageBreakAfter) in paragraphs)
            {
                var run = new W.Run(new W.Text(text));
                if (pageBreakAfter)
                {
                    run.AppendChild(new W.Break { Type = W.BreakValues.Page });
                }
                body.AppendChild(new W.Paragraph(run));
            }
            doc.AddMainDocumentPart().Document = new W.Document(body);
        }
        return stream.ToArray().ToMemoryFile(Regira.Office.MimeTypes.ContentTypes.DOCX);
    }

    private static string[] PageTexts(byte[] pdf)
    {
        using var reader = Docnet.Core.DocLib.Instance.GetDocReader(pdf, new Docnet.Core.Models.PageDimensions(1d));
        return Enumerable.Range(0, reader.GetPageCount())
            .Select(i =>
            {
                using var page = reader.GetPageReader(i);
                return page.GetText();
            })
            .ToArray();
    }

    /// <summary>
    /// The self-inclusion guard is per call: one long-lived instance keeps inserting documents past the
    /// nesting limit, summed over its calls.
    /// </summary>
    public virtual async Task Nested_Documents_Do_Not_Wear_Out_The_Service()
    {
        var creator = RequireCreator();
        for (var i = 0; i < 101; i++)
        {
            var input = TemplateInput("nested_templates.docx");
            input.DocumentParameters = new Dictionary<string, WordTemplateInput>
            {
                ["doc1"] = TemplateInput("parameters.docx"),
                ["doc2"] = TemplateInput("parameters.docx")
            };

            using var output = await creator.Create(input);
        }
    }

    public virtual void A_Template_That_Includes_Itself_Fails()
    {
        var creator = RequireCreator();
        var nested = TemplateInput("nested_templates.docx");
        nested.DocumentParameters = new Dictionary<string, WordTemplateInput> { ["doc1"] = nested };
        var header = TemplateInput("lorem_ipsum.docx");
        header.Headers!.Add(new WordHeaderFooterInput { Template = header });

        var viaNesting = Assert.ThrowsAsync<InvalidOperationException>(() => creator.Create(nested));
        var viaHeader = Assert.ThrowsAsync<InvalidOperationException>(() => creator.Create(header));

        Assert.Multiple(() =>
        {
            Assert.That(viaNesting!.Message, Does.Contain("Maximum insertable documents"));
            Assert.That(viaHeader!.Message, Does.Contain("Maximum insertable documents"));
        });
    }

    public virtual async Task A_Missing_Collection_Table_Leaves_The_Others()
    {
        var input = TemplateInput("template_row.docx");
        input.CollectionParameters!.Add("Not_In_The_Template", TemplateRows());
        input.CollectionParameters!.Add("Template_Table", TemplateRows());

        using var output = await RequireCreator().Create(input);

        Assert.That(await HasContent(output, "Item #12"), Is.True);
    }

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
        var tagLeft = await HasContent(output, "{{ title }}");
        var dateWritten = await HasContent(output, "16/09/2026");

        Assert.Multiple(() =>
        {
            Assert.That(tagLeft, Is.False, "a null value is written as empty text");
            Assert.That(dateWritten, Is.True);
        });
    }

    public virtual async Task Convert_To(FileFormat format, string outputName)
    {
        using var output = await RequireConverter().Convert(TemplateInput("template.docx"), format);
        await AssertSaved(output, OutputPath(outputName));
    }

    public virtual async Task From_A3_To_Pdf()
    {
        using var output = await RequireConverter().Convert(TemplateInput("template_a3.docx"), FileFormat.Pdf);
        await AssertSaved(output, OutputPath("converted_a3.pdf"));
    }

    public virtual async Task From_A4_To_Pdf_A3()
    {
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = PageSize.A3 }
        };

        using var output = await RequireConverter().Convert(TemplateInput("template.docx"), options);
        await AssertSaved(output, OutputPath("from_a4_to_a3.pdf"));
    }

    public virtual async Task To_Images()
    {
        var images = (await RequireToImages().ToImages(TemplateInput("template.docx"))).ToArray();

        ClassicAssert.Greater(images.Length, 0);
        for (var i = 0; i < images.Length; i++)
        {
            await AssertSaved(images[i], OutputPath($"image-{i + 1}.jpg"));
            images[i].Dispose();
        }
    }

    public virtual async Task GetImages()
    {
        var images = (await RequireImageExtractor().GetImages(TemplateInput("template.docx"))).ToList();

        ClassicAssert.Greater(images.Count, 0);
        Assert.That(images.All(x => (x.File?.GetBytes()?.Length ?? 0) > 0), Is.True);
    }

    public virtual async Task GetText()
    {
        var text = await RequireTextExtractor().GetText(TemplateInput("lorem_ipsum.docx"));

        Assert.That(text, Is.Not.Empty);
        Assert.That(text, Does.Contain("Lorem ipsum"));
    }


    private IWordConverter RequireConverter()
    {
        if (Converter == null) Assert.Ignore("Conversion not supported");
        return Converter!;
    }
    private IWordMerger RequireMerger()
    {
        if (Merger == null) Assert.Ignore("Merging not supported");
        return Merger!;
    }
    private IWordCreator RequireCreator()
    {
        if (Creator == null) Assert.Ignore("Creating not supported");
        return Creator!;
    }
    private IWordToImagesService RequireToImages()
    {
        if (ToImagesService == null) Assert.Ignore("Page rendering not supported");
        return ToImagesService!;
    }
    private IWordImageExtractor RequireImageExtractor()
    {
        if (ImageExtractor == null) Assert.Ignore("Image extraction not supported");
        return ImageExtractor!;
    }
    private IWordTextExtractor RequireTextExtractor()
    {
        if (TextExtractor == null) Assert.Ignore("Text extraction not supported");
        return TextExtractor!;
    }

    private static ICollection<IDictionary<string, object>> TemplateRows() =>
    [
        DictionaryUtility.ToDictionary(new { Id = "10", Title = "Item #10", Price = 100 })!,
        DictionaryUtility.ToDictionary(new { Id = "12", Title = "Item #12", Price = 200 })!,
        DictionaryUtility.ToDictionary(new { Id = "31", Title = "Item #31", Price = 350 })!
    ];
}
