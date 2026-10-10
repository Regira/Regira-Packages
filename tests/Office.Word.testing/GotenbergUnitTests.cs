using System.Net;
using System.Text;
using DocumentFormat.OpenXml.Packaging;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Http;
using Office.Word.testing.Abstractions;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Clients.DependencyInjection;
using Regira.Office.MimeTypes;
using Regira.Office.Models;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Gotenberg;
using Regira.Office.Word.Gotenberg.DependencyInjection;
using Regira.Office.Word.Gotenberg.Internal;
using Regira.Office.Word.Models;
using A = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Office.Word.testing;

/// <summary>
/// Everything about the Gotenberg backend that needs no server: the checks made before a request, the
/// request itself (against a stub handler), the upload format, the page setup written into the
/// document, and the DI registration.
/// </summary>
[TestFixture]
public class GotenbergUnitTests() : WordAssetsTestsBase("Gotenberg")
{
    private static readonly byte[] PdfBytes = Encoding.ASCII.GetBytes("%PDF-1.7 stub");

    // ---- checks made before any request ----

    [TestCase(FileFormat.Docx)]
    [TestCase(FileFormat.Doc)]
    [TestCase(FileFormat.Dotx)]
    [TestCase(FileFormat.Dot)]
    [TestCase(FileFormat.Docm)]
    [TestCase(FileFormat.Dotm)]
    [TestCase(FileFormat.Html)]
    [TestCase(FileFormat.Rtf)]
    [TestCase(FileFormat.Odt)]
    [TestCase(FileFormat.EPub)]
    public async Task Convert_To_Anything_But_Pdf_Is_Not_Supported(FileFormat format)
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(TemplateInput("template.docx"), format));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("PDF only"));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [TestCase(FileFormat.Png)]
    [TestCase(FileFormat.Jpeg)]
    public async Task Convert_To_Image_Points_At_ToImages(FileFormat format)
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(TemplateInput("template.docx"), format));

        Assert.That(ex!.Message, Does.Contain("ToImages"));
    }

    [Test]
    public async Task Template_Input_Without_Creator_Is_Rejected()
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "A title" };
        input.Headers = [new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") }];

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(input, FileFormat.Pdf));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(nameof(WordTemplateInput.GlobalParameters)));
            Assert.That(ex.Message, Does.Contain(nameof(WordTemplateInput.Headers)));
            Assert.That(ex.Message, Does.Contain(nameof(IWordCreator)));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task Settings_On_A_Non_OpenXml_Source_Are_Rejected()
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());
        var options = new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings() };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(TemplateInput("template.odt"), options));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(".odt"));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    [Test]
    public async Task ToImages_Without_A_PdfToImageService_Throws()
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => service.ToImages(TemplateInput("template.docx")));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain(nameof(IPdfToImageService)));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    // ---- the request ----

    [Test]
    public async Task A_Finished_Document_Is_Uploaded_As_Is()
    {
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient());
        var template = ReadAsset("template.docx");

        // a default-initialised input carries empty collections and default options: nothing to render
        using var output = await service.Convert(new WordTemplateInput { Template = template }, FileFormat.Pdf);

        var request = handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Method, Is.EqualTo(HttpMethod.Post));
            Assert.That(request.Uri.AbsolutePath, Is.EqualTo("/" + WordService.ConvertRoute));
            Assert.That(request.Uploads.Single().Name, Is.EqualTo("files"));
            Assert.That(request.Uploads.Single().FileName, Is.EqualTo("document.docx"));
            Assert.That(request.Uploads.Single().Bytes, Is.EqualTo(template.GetBytes()));
            Assert.That(output.GetBytes(), Is.EqualTo(PdfBytes));
            Assert.That(output.ContentType, Is.EqualTo(ContentTypes.PDF));
        });
    }

    [Test]
    public async Task A_Template_Input_Is_Rendered_By_The_Creator_First()
    {
        var rendered = ReadAsset("lorem_ipsum.docx").GetBytes()!;
        var creator = new StubCreator(rendered);
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient(), creator: creator);
        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "A title" };

        using var _ = await service.Convert(input, FileFormat.Pdf);

        var upload = handler.Requests.Single().Uploads.Single();
        Assert.Multiple(() =>
        {
            Assert.That(creator.Inputs, Is.EqualTo(new[] { input }));
            Assert.That(upload.FileName, Is.EqualTo("document.docx"));
            Assert.That(upload.Bytes, Is.EqualTo(rendered));
        });
    }

    /// <summary>
    /// A condition on a key the input does not give is false, and a loop over one has no rows, so a template holding a
    /// block needs rendering even without a single parameter.
    /// </summary>
    [TestCase("Intro", "{{#if IsDraft}}", "DRAFT", "{{/if}}")]
    [TestCase("Intro", "{{#each Lines}}", "{{Description}}", "{{/each}}")]
    public async Task A_Template_With_Blocks_Needs_A_Creator(params string[] paragraphs)
    {
        var handler = new StubHandler();
        var service = new WordService(handler.CreateClient());
        var input = new WordTemplateInput { Template = Docx.Document(paragraphs) };

        var ex = await Assert.ThrowsAsync<NotSupportedException>(() => service.Convert(input, FileFormat.Pdf));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("template blocks"));
            Assert.That(handler.Requests, Is.Empty);
        });
    }

    /// <summary>
    /// Every OOXML conversion is scanned in-process for blocks, so a part beyond the scan's limit — a zip bomb among them —
    /// is not read, and the document goes to Gotenberg as it is.
    /// </summary>
    [Test]
    public void A_Part_Beyond_The_Scan_Limit_Is_Not_Read()
    {
        var template = Docx.Document(new string('x', 2_000), "{{#if IsDraft}}", "DRAFT", "{{/if}}").GetBytes()!;

        Assert.Multiple(() =>
        {
            Assert.That(TemplateMarkers.Any(template), Is.True, "within the limit");
            Assert.That(TemplateMarkers.Any(template, maxBytes: 1_000), Is.False, "beyond it");
        });
    }

    /// <summary>
    /// A document that opens a block is created in-process, which loads every header and footer whole, so the limit counts
    /// them, past the block found first: a header too large for it sends the document to Gotenberg.
    /// </summary>
    [Test]
    public void The_Limit_Counts_The_Headers()
    {
        var template = Docx.Document(Docx.Paragraphs("{{#if IsDraft}}", "DRAFT", "{{/if}}"), header: Docx.Paragraphs(new string('x', 20_000))).GetBytes()!;
        var bodyOnly = Docx.Document("{{#if IsDraft}}", "DRAFT", "{{/if}}").GetBytes()!;

        Assert.Multiple(() =>
        {
            Assert.That(TemplateMarkers.Any(bodyOnly, maxBytes: 10_000), Is.True, "the body fits the limit");
            Assert.That(TemplateMarkers.Any(template, maxBytes: 10_000), Is.False, "the body and its header do not");
        });
    }

    /// <summary>
    /// A creator other than Word.Mini loads every part, so the limit counts the footnotes too, though no marker there counts.
    /// </summary>
    [Test]
    public void The_Limit_Counts_Every_Part()
    {
        var body = Docx.Paragraphs("{{#if IsDraft}}", "DRAFT", "{{/if}}").ToArray();
        var smallFootnote = Docx.Document(body.Select(x => (W.Paragraph)x.CloneNode(true)), footnote: Docx.Paragraphs("Note")).GetBytes()!;
        var largeFootnote = Docx.Document(body.Select(x => (W.Paragraph)x.CloneNode(true)), footnote: Docx.Paragraphs(new string('x', 20_000))).GetBytes()!;

        Assert.Multiple(() =>
        {
            Assert.That(TemplateMarkers.Any(smallFootnote, maxBytes: 10_000), Is.True, "the document fits the limit");
            Assert.That(TemplateMarkers.Any(largeFootnote, maxBytes: 10_000), Is.False, "its footnotes do not");
        });
    }

    /// <summary>
    /// Opening a package parses its relationships and content types whole, before any part is scanned, so the limit is
    /// judged first, on the sizes the zip declares: a relationship target inflating far beyond it is never read.
    /// </summary>
    [Test]
    public void A_Package_Declaring_More_Than_The_Limit_Is_Not_Opened()
    {
        var template = Docx.Document("{{#if IsDraft}}", "DRAFT", "{{/if}}").GetBytes()!;
        // an external hyperlink whose target inflates to 16 MB, from a few KB in the zip
        var bomb = RewriteEntry(template, "_rels/.rels", xml => xml.Replace("</Relationships>",
            "<Relationship Id=\"rIdBomb\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/hyperlink\""
            + $" Target=\"https://example.com/{new string('a', 16 * 1024 * 1024)}\" TargetMode=\"External\"/></Relationships>"));

        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var opensBlock = TemplateMarkers.Any(bomb, maxBytes: 1024 * 1024);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

        Assert.Multiple(() =>
        {
            Assert.That(bomb, Has.Length.LessThan(100_000), "the bomb is small");
            Assert.That(opensBlock, Is.False);
            Assert.That(allocated, Is.LessThan(1024 * 1024), "its relationships were not read");
        });
    }

    /// <summary>
    /// A size declared smaller than the part inflates to is no way around the limit: the zip reader stops each part at its
    /// declared size, so the part is read cut short, and the document, unreadable, holds no block.
    /// </summary>
    [Test]
    public void A_Part_Declared_Smaller_Than_It_Inflates_Is_Read_Cut_Short()
    {
        // the marker after 4 MB of text, which the zip compresses to a few KB
        var template = Docx.Document(new string('x', 4 * 1024 * 1024), "{{#if IsDraft}}", "DRAFT", "{{/if}}").GetBytes()!;
        var forged = DeclareSize(template, "word/document.xml", 10_000);

        var allocated = GC.GetAllocatedBytesForCurrentThread();
        var opensBlock = TemplateMarkers.Any(forged, maxBytes: 1024 * 1024);
        allocated = GC.GetAllocatedBytesForCurrentThread() - allocated;

        Assert.Multiple(() =>
        {
            Assert.That(TemplateMarkers.Any(template, maxBytes: 1024 * 1024), Is.False, "declared as it is, it exceeds the limit");
            Assert.That(opensBlock, Is.False);
            Assert.That(allocated, Is.LessThan(1024 * 1024), "no more than the declared size was read");
        });
    }

    /// <summary>
    /// Opening a package builds an object for every part, so a package holding more parts than a document has is not
    /// opened, however little it declares.
    /// </summary>
    [Test]
    public void A_Package_With_More_Parts_Than_A_Document_Has_Is_Not_Opened()
    {
        var template = Docx.Document("{{#if IsDraft}}", "DRAFT", "{{/if}}").GetBytes()!;
        byte[] crowded;
        using (var stream = new MemoryStream())
        {
            stream.Write(template);
            using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Update, true))
            {
                // empty, unreferenced XML parts: the package would open with them, and its block be found
                for (var i = 0; i < TemplateMarkers.MaxParts; i++)
                {
                    zip.CreateEntry($"extra/part{i}.xml");
                }
            }
            crowded = stream.ToArray();
        }

        Assert.Multiple(() =>
        {
            Assert.That(TemplateMarkers.Any(template), Is.True, "the document");
            Assert.That(TemplateMarkers.Any(crowded), Is.False, "the document among a thousand empty parts");
        });
    }

    /// <summary>
    /// A package the SDK cannot read — here one relating an image the zip does not hold, as some editors leave behind —
    /// holds no block the scan can vouch for, and is uploaded as it is, for Gotenberg to convert.
    /// </summary>
    [Test]
    public async Task A_Package_The_Scan_Cannot_Read_Is_Uploaded_As_Is()
    {
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient());
        var document = Docx.Document(Docx.Paragraphs("Intro"), header: Docx.Paragraphs("Header")).GetBytes()!;
        var dangling = RewriteEntry(document, "word/_rels/document.xml.rels", xml => xml.Replace("</Relationships>",
            "<Relationship Id=\"rIdMissing\" Type=\"http://schemas.openxmlformats.org/officeDocument/2006/relationships/image\""
            + " Target=\"media/image1.png\"/></Relationships>"));

        using var _ = await service.Convert(new WordTemplateInput { Template = dangling.ToMemoryFile(ContentTypes.DOCX) }, FileFormat.Pdf);

        Assert.That(handler.Requests.Single().Uploads.Single().Bytes, Is.EqualTo(dangling));
    }

    private static byte[] RewriteEntry(byte[] package, string entryName, Func<string, string> rewrite)
    {
        using var stream = new MemoryStream();
        stream.Write(package);
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry(entryName)!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }
            entry.Delete();
            using var writer = new StreamWriter(zip.CreateEntry(entryName).Open());
            writer.Write(rewrite(xml));
        }
        return stream.ToArray();
    }

    // overwrites the uncompressed size an entry declares, in its local header and in the central directory
    private static byte[] DeclareSize(byte[] package, string entryName, uint size)
    {
        var bytes = (byte[])package.Clone();
        var name = Encoding.UTF8.GetBytes(entryName);
        for (var i = 0; i + 4 <= bytes.Length; i++)
        {
            var (sizeOffset, nameLengthOffset, nameOffset) = BitConverter.ToUInt32(bytes, i) switch
            {
                0x04034b50 => (22, 26, 30), // local file header
                0x02014b50 => (24, 28, 46), // central directory file header
                _ => (-1, 0, 0)
            };
            if (sizeOffset < 0 || i + nameOffset + name.Length > bytes.Length
                || BitConverter.ToUInt16(bytes, i + nameLengthOffset) != name.Length
                || !bytes.AsSpan(i + nameOffset, name.Length).SequenceEqual(name))
            {
                continue;
            }
            BitConverter.TryWriteBytes(bytes.AsSpan(i + sizeOffset, 4), size);
        }
        return bytes;
    }

    /// <summary>
    /// The scan reads a part through its own stream: a DTD there is refused, as the SDK refuses it, so an entity cannot
    /// expand into a marker or into anything else.
    /// </summary>
    [Test]
    public void The_Scan_Refuses_A_Dtd()
    {
        var template = Docx.Document("{{#if IsDraft}}", "DRAFT", "{{/if}}").GetBytes()!;
        using var stream = new MemoryStream();
        stream.Write(template);
        using (var zip = new System.IO.Compression.ZipArchive(stream, System.IO.Compression.ZipArchiveMode.Update, true))
        {
            var entry = zip.GetEntry("word/document.xml")!;
            string xml;
            using (var reader = new StreamReader(entry.Open()))
            {
                xml = reader.ReadToEnd();
            }
            entry.Delete();
            // the marker exists only once the entity expands
            xml = xml.Replace("{{#if IsDraft}}", "&marker;");
            xml = xml.Insert(xml.IndexOf("<w:document", StringComparison.Ordinal), "<!DOCTYPE w:document [<!ENTITY marker \"{{#if IsDraft}}\">]>");
            using var writer = new StreamWriter(zip.CreateEntry("word/document.xml").Open());
            writer.Write(xml);
        }

        Assert.That(TemplateMarkers.Any(stream.ToArray()), Is.False);
    }

    /// <summary>
    /// The scan streams each part rather than loading it, and reads a paragraph as the creators do: a block opens in a
    /// header, in a text box — whose paragraphs are read apart from the one anchoring it — and after an empty paragraph.
    /// </summary>
    [Test]
    public void The_Scan_Finds_A_Block_In_A_Header_A_Text_Box_And_After_An_Empty_Paragraph()
    {
        var inHeader = Docx.Document([Docx.Paragraph("Body")], header: Docx.Paragraphs("{{#if IsDraft}}", "DRAFT", "{{/if}}"));
        var inTextBox = Docx.Document([Docx.TextBox("{{#if IsDraft}}", "DRAFT", "{{/if}}")]);
        var afterEmpty = Docx.Document([new W.Paragraph(), .. Docx.Paragraphs("{{#if IsDraft}}", "DRAFT", "{{/if}}")]);

        Assert.Multiple(() =>
        {
            Assert.That(TemplateMarkers.Any(inHeader.GetBytes()!), Is.True, "in a header");
            Assert.That(TemplateMarkers.Any(inTextBox.GetBytes()!), Is.True, "in a text box");
            Assert.That(TemplateMarkers.Any(afterEmpty.GetBytes()!), Is.True, "after an empty paragraph");
        });
    }

    [Test]
    public async Task A_Template_With_Conditional_Blocks_Is_Rendered_By_The_Creator_First()
    {
        var rendered = ReadAsset("lorem_ipsum.docx").GetBytes()!;
        var creator = new StubCreator(rendered);
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient(), creator: creator);
        var input = new WordTemplateInput { Template = Docx.Document("Intro", "{{#if IsDraft}}", "DRAFT", "{{/if}}") };

        using var _ = await service.Convert(input, FileFormat.Pdf);

        Assert.Multiple(() =>
        {
            Assert.That(creator.Inputs, Is.EqualTo(new[] { input }));
            Assert.That(handler.Requests.Single().Uploads.Single().Bytes, Is.EqualTo(rendered));
        });
    }

    /// <summary>
    /// A marker counts in a paragraph's visible text only, as the creators read it: one in a deleted revision or a
    /// field code is not a block.
    /// </summary>
    [Test]
    public async Task Markers_Outside_The_Visible_Text_Need_No_Creator()
    {
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient());
        var template = Docx.Document([
            Docx.Paragraph("Intro"),
            new W.Paragraph(new W.DeletedRun(new W.Run(new W.DeletedText("{{#if IsDraft}}")))),
            new W.Paragraph(new W.Run(new W.FieldCode(" QUOTE \"{{/if}}\" ")))
        ]);

        using var _ = await service.Convert(new WordTemplateInput { Template = template }, FileFormat.Pdf);

        Assert.That(handler.Requests.Single().Uploads.Single().Bytes, Is.EqualTo(template.GetBytes()));
    }

    /// <summary>
    /// Only a paragraph that opens a block makes a document use blocks, as the creators decide it: a finished document
    /// that writes about templates converts as it is — marker text among other text, another template language's
    /// tags on lines of their own, a stray <c>{{/if}}</c>.
    /// </summary>
    [TestCase("Intro", "Wrap optional text in {{#if Key}} and {{/if}}.")]
    [TestCase("A Go template sample:", "{{range .Items}}", "{{.Name}}", "{{else}}", "No items.", "{{end}}")]
    [TestCase("A block closes with", "{{/if}}")]
    public async Task A_Document_That_Opens_No_Block_Needs_No_Creator(params string[] paragraphs)
    {
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient());
        var template = Docx.Document(paragraphs);

        using var _ = await service.Convert(new WordTemplateInput { Template = template }, FileFormat.Pdf);

        Assert.That(handler.Requests.Single().Uploads.Single().Bytes, Is.EqualTo(template.GetBytes()));
    }

    [Test]
    public async Task A_Finished_Document_Skips_The_Creator()
    {
        var creator = new StubCreator([]);
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient(), creator: creator);

        using var _ = await service.Convert(TemplateInput("template.odt"), FileFormat.Pdf);

        Assert.Multiple(() =>
        {
            // the creators have no ODT reader; a document that needs no rendering must not reach them
            Assert.That(creator.Inputs, Is.Empty);
            Assert.That(handler.Requests.Single().Uploads.Single().FileName, Is.EqualTo("document.odt"));
        });
    }

    [Test]
    public async Task Page_Settings_Are_Written_Before_Upload()
    {
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient());
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = PageSize.A3 }
        };

        using var _ = await service.Convert(TemplateInput("template.docx"), options);

        var pageSize = PageSizes(handler.Requests.Single().Uploads.Single().Bytes).Single();
        Assert.That(pageSize, Is.EqualTo((16838u, 23811u)));
    }

    [Test]
    public async Task ToImages_Rasterises_The_Converted_Pdf()
    {
        var rasteriser = new StubPdfToImageService();
        var imageOptions = new PdfToImagesOptions { Format = Regira.Media.Drawing.Enums.ImageFormat.Png };
        var handler = new StubHandler(_ => Pdf());
        var service = new WordService(handler.CreateClient(), new GotenbergWordConfig { ImageOptions = imageOptions }, rasteriser);

        var images = (await service.ToImages(TemplateInput("template.docx"))).ToList();

        Assert.Multiple(() =>
        {
            Assert.That(images, Has.Count.EqualTo(2));
            Assert.That(rasteriser.Pdfs.Single(), Is.EqualTo(PdfBytes));
            Assert.That(rasteriser.Options.Single(), Is.SameAs(imageOptions));
        });
    }

    [Test]
    public async Task A_Server_Error_Surfaces_Status_And_Body()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
        {
            Content = new StringContent("context deadline exceeded")
        });
        var service = new WordService(handler.CreateClient());

        var ex = await Assert.ThrowsAsync<HttpRequestException>(() => service.Convert(TemplateInput("template.docx"), FileFormat.Pdf));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.StatusCode, Is.EqualTo(HttpStatusCode.ServiceUnavailable));
            Assert.That(ex.Message, Does.Contain("503"));
            Assert.That(ex.Message, Does.Contain("context deadline exceeded"));
            Assert.That(ex.Message, Does.Contain("--api-timeout"));
        });
    }

    // ---- the upload format ----

    [TestCase("template.docx", "docx")]
    [TestCase("template.doc", "doc")]
    [TestCase("template.dot", "doc")]
    [TestCase("template.odt", "odt")]
    [TestCase("template.html", "html")]
    public void SourceFormat_Is_Read_From_The_Content(string filename, string expected)
    {
        var bytes = ReadAsset(filename).GetBytes()!;

        Assert.That(SourceFormat.Resolve(bytes.ToBinaryFile(), bytes), Is.EqualTo(expected));
    }

    [Test]
    public void SourceFormat_Recognises_Rtf()
    {
        var bytes = Encoding.ASCII.GetBytes(@"{\rtf1\ansi Hello}");

        Assert.That(SourceFormat.Sniff(bytes), Is.EqualTo("rtf"));
    }

    [Test]
    public void SourceFormat_Prefers_The_File_Name()
    {
        var bytes = ReadAsset("template.docx").GetBytes()!;
        var file = new BinaryFileItem { Bytes = bytes, Length = bytes.Length, FileName = "Letter.DOTX" };

        Assert.That(SourceFormat.Resolve(file, bytes), Is.EqualTo("dotx"));
    }

    [Test]
    public void SourceFormat_Ignores_A_File_Name_Outside_The_Route()
    {
        var bytes = ReadAsset("template.docx").GetBytes()!;
        var file = new BinaryFileItem { Bytes = bytes, Length = bytes.Length, FileName = "upload.tmp" };

        Assert.That(SourceFormat.Resolve(file, bytes), Is.EqualTo("docx"));
    }

    [Test]
    public void SourceFormat_Rejects_Unrecognised_Content()
    {
        var bytes = ReadAsset("sample1.jpg").GetBytes()!;

        var ex = Assert.Throws<NotSupportedException>(() => SourceFormat.Resolve(bytes.ToBinaryFile(), bytes));

        Assert.That(ex!.Message, Does.Contain(nameof(INamedFile)));
    }

    // ---- page setup ----

    [TestCase(PageSize.A3, 16838u, 23811u)]
    [TestCase(PageSize.A4, 11906u, 16838u)]
    [TestCase(PageSize.A5, 8391u, 11906u)]
    [TestCase(PageSize.A6, 5953u, 8391u)]
    public void PageSizes_Match_Word(PageSize size, uint width, uint height)
    {
        Assert.That(Regira.Office.Word.Layout.WordPageSizes.Twips(size), Is.EqualTo((width, height)));
    }

    [Test]
    public void PageSetup_Resizes_The_Page()
    {
        var output = Apply("template.docx", new DocumentSettings { PageSize = PageSize.A3 });

        using var doc = Open(output);
        var pageSize = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageSize>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(pageSize.Width!.Value, Is.EqualTo(16838u));
            Assert.That(pageSize.Height!.Value, Is.EqualTo(23811u));
            Assert.That(pageSize.Orient!.Value, Is.EqualTo(W.PageOrientationValues.Portrait));
        });
    }

    [Test]
    public void PageSetup_Landscape_Swaps_Width_And_Height()
    {
        var output = Apply("template.docx", new DocumentSettings { PageOrientation = PageOrientation.Landscape });

        using var doc = Open(output);
        var pageSize = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageSize>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(pageSize.Width!.Value, Is.EqualTo(16838u));
            Assert.That(pageSize.Height!.Value, Is.EqualTo(11906u));
            Assert.That(pageSize.Orient!.Value, Is.EqualTo(W.PageOrientationValues.Landscape));
        });
    }

    [Test]
    public void PageSetup_Writes_Margins_In_Twips()
    {
        var output = Apply("template.docx", new DocumentSettings { Margins = 36f });

        using var doc = Open(output);
        var margin = doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageMargin>().Single();
        Assert.Multiple(() =>
        {
            Assert.That(margin.Top!.Value, Is.EqualTo(720));
            Assert.That(margin.Bottom!.Value, Is.EqualTo(720));
            Assert.That(margin.Left!.Value, Is.EqualTo(720u));
            Assert.That(margin.Right!.Value, Is.EqualTo(720u));
            // untouched by the settings
            Assert.That(margin.Header!.Value, Is.EqualTo(708u));
        });
    }

    [Test]
    public void PageSetup_Stops_A_Picture_At_The_Largest_Shape_Word_Holds()
    {
        const long maxShapeSize = 1584 * 12700; // 22 inches in EMUs
        var input = ReadAsset("template_image.docx").GetBytes()!;
        var before = PictureExtents(input);

        var output = Apply("template_image.docx", new DocumentSettings { PageSize = PageSize.A0 });

        // A4 → A0 with 1417 twip side margins
        var scale = (Regira.Office.Word.Layout.WordPageSizes.Twips(PageSize.A0).Width - 2834d) / (11906d - 2834);
        var after = PictureExtents(output);
        Assert.That(before.Max(extent => Math.Max(extent.Cx, extent.Cy)) * scale, Is.GreaterThan(maxShapeSize), "scaled with the text width alone, the picture would pass the limit");
        Assert.Multiple(() =>
        {
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(Math.Max(after[i].Cx, after[i].Cy), Is.EqualTo(maxShapeSize).Within(1), "at the limit");
                Assert.That((double)after[i].Cy / after[i].Cx, Is.EqualTo((double)before[i].Cy / before[i].Cx).Within(1).Percent, "height over width");
            }
        });
    }

    [Test]
    public void PageSetup_Scales_Pictures_With_The_Text_Width()
    {
        var input = ReadAsset("template_image.docx").GetBytes()!;
        var before = PictureExtents(input);

        var output = Apply("template_image.docx", new DocumentSettings { PageSize = PageSize.A3 });

        // A4 → A3 with 1417 twip side margins: (16838 − 2834) / (11906 − 2834)
        var scale = (16838d - 2834) / (11906d - 2834);
        var after = PictureExtents(output);
        // the picture's wp:extent and its a:ext
        Assert.That(before, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i].Cx, Is.EqualTo(before[i].Cx * scale).Within(1));
                Assert.That(after[i].Cy, Is.EqualTo(before[i].Cy * scale).Within(1));
            }
        });
    }

    /// <summary>A picture in a text box scales once, as the text box does: as far as the text width grows.</summary>
    [Test]
    public void PageSetup_Scales_A_Picture_In_A_Text_Box_Once()
    {
        var input = Docx.Document([new W.Paragraph(new W.Run(new W.Drawing(TextBoxHoldingAPicture)))]).GetBytes()!;
        var before = PictureExtents(input);

        var output = OpenXmlPageSetup.Apply(input, new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings { PageSize = PageSize.A3 } });

        // A4 → A3, the page without margins
        var scale = 16838d / 11906;
        var after = PictureExtents(output);
        // the text box's wp:extent, then the picture's; then their a:ext
        Assert.That(before, Has.Count.EqualTo(4));
        Assert.Multiple(() =>
        {
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i].Cx, Is.EqualTo(before[i].Cx * scale).Within(1));
                Assert.That(after[i].Cy, Is.EqualTo(before[i].Cy * scale).Within(1));
            }
        });
    }

    /// <summary>
    /// A group scales as a whole, through its own size: its shapes sit in the child space that its a:chOff and a:chExt map
    /// onto its a:ext, so they follow it and keep their own a:off and a:ext.
    /// </summary>
    /// <summary>
    /// A picture in a text box grows no more than the text box does: where the box stops at the 22-inch limit, the
    /// picture keeps its share of the box, where its own limit would have let it grow as large.
    /// </summary>
    [Test]
    public void PageSetup_Grows_A_Picture_In_A_Text_Box_No_More_Than_The_Box()
    {
        var input = Docx.Document([new W.Paragraph(new W.Run(new W.Drawing(TextBoxHolding(450, 100, 400, 80))))]).GetBytes()!;

        // A4 → A0, the page without margins: four times as wide
        var output = OpenXmlPageSetup.Apply(input, new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings { PageSize = PageSize.A0 } });

        // the text box's wp:extent, then the picture's
        var after = PictureExtents(output);
        var boxScale = 1584d / 450;
        Assert.Multiple(() =>
        {
            Assert.That(after[0].Cx, Is.EqualTo(1584 * 12700).Within(1), "the box at the limit");
            Assert.That(after[1].Cx, Is.EqualTo(400 * 12700 * boxScale).Within(1), "the picture grown as the box");
            Assert.That(after[1].Cy, Is.EqualTo(80 * 12700 * boxScale).Within(1));
        });
    }

    /// <summary>
    /// A drawing canvas scales with its shapes: it places them in EMUs of its own, with no child space to map them
    /// through as a group has, so each shape's offset and size grow with the canvas.
    /// </summary>
    [Test]
    public void PageSetup_Scales_A_Drawing_Canvas_With_Its_Shapes()
    {
        var input = Docx.Document([new W.Paragraph(new W.Run(new W.Drawing(CanvasOfTwoShapes)))]).GetBytes()!;
        var before = ShapeTransforms(input);

        var output = OpenXmlPageSetup.Apply(input, new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings { PageSize = PageSize.A3 } });

        // A4 → A3, the page without margins
        var scale = 16838d / 11906;
        var after = ShapeTransforms(output);
        using var doc = Open(output);
        var canvas = doc.MainDocumentPart!.Document!.Body!.Descendants<Wp.Extent>().Single();
        Assert.That(before, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(canvas.Cx!.Value, Is.EqualTo(3810000 * scale).Within(1), "the canvas");
            for (var i = 0; i < before.Count; i++)
            {
                Assert.That(after[i].X, Is.EqualTo(before[i].X * scale).Within(1), $"shape {i + 1}'s offset");
                Assert.That(after[i].Y, Is.EqualTo(before[i].Y * scale).Within(1), $"shape {i + 1}'s offset");
                Assert.That(after[i].Cx, Is.EqualTo(before[i].Cx * scale).Within(1), $"shape {i + 1}'s size");
                Assert.That(after[i].Cy, Is.EqualTo(before[i].Cy * scale).Within(1), $"shape {i + 1}'s size");
            }
        });
    }

    [Test]
    public void PageSetup_Scales_A_Group_Of_Shapes_As_A_Whole()
    {
        var input = Docx.Document([Docx.GroupedTextBoxes(["A"], ["B"])]).GetBytes()!;
        var before = Group(input);

        var output = OpenXmlPageSetup.Apply(input, new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = new DocumentSettings { PageSize = PageSize.A3 } });

        // A4 → A3, the page without margins
        var scale = 16838d / 11906;
        var after = Group(output);
        Assert.That(before.Shapes, Has.Count.EqualTo(2));
        Assert.Multiple(() =>
        {
            Assert.That(after.Extent.Cx, Is.EqualTo(before.Extent.Cx * scale).Within(1), "wp:extent");
            Assert.That(after.Extent.Cy, Is.EqualTo(before.Extent.Cy * scale).Within(1), "wp:extent");
            Assert.That(after.Extents.Cx, Is.EqualTo(before.Extents.Cx * scale).Within(1), "the group's a:ext");
            Assert.That(after.Extents.Cy, Is.EqualTo(before.Extents.Cy * scale).Within(1), "the group's a:ext");
            Assert.That(after.ChildSpace, Is.EqualTo(before.ChildSpace), "the group's a:chOff and a:chExt");
            Assert.That(after.Shapes, Is.EqualTo(before.Shapes), "the shapes' a:off and a:ext");
        });
    }

    [Test]
    public void PageSetup_Scales_Tables_Spanning_The_Text_Width()
    {
        var input = ReadAsset("template.docx").GetBytes()!;
        var before = GridColumns(input);

        var output = Apply("template.docx", new DocumentSettings { PageSize = PageSize.A3 });

        var scale = (16838d - 2834) / (11906d - 2834);
        var after = GridColumns(output);
        Assert.That(after, Is.EqualTo(before.Select(width => Math.Round(width * scale))));
    }

    [Test]
    public void PageSetup_Leaves_Content_Alone_When_AutoScale_Is_Off()
    {
        var input = ReadAsset("template.docx").GetBytes()!;
        var options = new ConversionOptions
        {
            OutputFormat = FileFormat.Pdf,
            Settings = new DocumentSettings { PageSize = PageSize.A3 },
            AutoScalePictures = false,
            AutoScaleTables = false
        };

        var output = OpenXmlPageSetup.Apply(input, options);

        Assert.Multiple(() =>
        {
            Assert.That(GridColumns(output), Is.EqualTo(GridColumns(input)));
            Assert.That(PictureExtents(output), Is.EqualTo(PictureExtents(input)));
        });
    }

    [Test]
    public void PageSetup_Without_Settings_Returns_The_Source()
    {
        var input = ReadAsset("template.docx").GetBytes()!;

        var output = OpenXmlPageSetup.Apply(input, new ConversionOptions { OutputFormat = FileFormat.Pdf });

        Assert.That(output, Is.SameAs(input));
    }

    // ---- registration ----

    [Test]
    public async Task AddGotenbergWord_Registers_Both_Capabilities_Against_The_Server()
    {
        var handler = new StubHandler(_ => Pdf());
        var services = new ServiceCollection();
        services.AddGotenbergWord(o =>
        {
            o.BaseUrl = "http://gotenberg.test:3000/root";
            o.Username = "user";
            o.Password = "secret";
        });
        UseHandler(services, handler);
        await using var provider = services.BuildServiceProvider();

        var converter = provider.GetRequiredService<IWordConverter>();
        using var _ = await converter.Convert(TemplateInput("template.docx"), FileFormat.Pdf);

        var request = handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(converter, Is.InstanceOf<WordService>());
            Assert.That(provider.GetRequiredService<IWordToImagesService>(), Is.InstanceOf<WordService>());
            Assert.That(request.Uri.ToString(), Is.EqualTo("http://gotenberg.test:3000/root/" + WordService.ConvertRoute));
            Assert.That(request.Authorization, Is.EqualTo("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("user:secret"))));
        });
    }

    [TestCase(true, TestName = "AddGotenbergWord_Keeps_Its_Client_Apart_From_AddOfficeClients(registered first)")]
    [TestCase(false, TestName = "AddGotenbergWord_Keeps_Its_Client_Apart_From_AddOfficeClients(registered last)")]
    public async Task AddGotenbergWord_Keeps_Its_Client_Apart_From_AddOfficeClients(bool gotenbergFirst)
    {
        // Gotenberg for Word, the Regira Office API for the rest — its IPdfToImageService feeds ToImages
        var handler = new StubHandler(_ => Pdf());
        var services = new ServiceCollection();
        void AddGotenberg() => services.AddGotenbergWord(o =>
        {
            o.BaseUrl = "http://gotenberg.test:3000";
            o.Username = "user";
            o.Password = "secret";
        });
        void AddOffice() => services.AddOfficeClients(o => o.BaseUrl = "http://office.test");
        if (gotenbergFirst)
        {
            AddGotenberg();
            AddOffice();
        }
        else
        {
            AddOffice();
            AddGotenberg();
        }
        UseHandler(services, handler);
        await using var provider = services.BuildServiceProvider();

        var gotenberg = provider.GetServices<IWordConverter>().OfType<WordService>().Single();
        using var _ = await gotenberg.Convert(TemplateInput("template.docx"), FileFormat.Pdf);
        // the client Office.Clients registers for the same interface
        var officeClient = provider.GetRequiredService<IHttpClientFactory>().CreateClient(nameof(IWordConverter));

        var request = handler.Requests.Single();
        Assert.Multiple(() =>
        {
            Assert.That(request.Uri.Host, Is.EqualTo("gotenberg.test"));
            Assert.That(request.Authorization, Does.StartWith("Basic "));
            Assert.That(officeClient.BaseAddress, Is.EqualTo(new Uri("http://office.test/")));
            Assert.That(officeClient.DefaultRequestHeaders.Authorization, Is.Null, "Gotenberg's credentials must not reach the Regira Office API");
        });
    }

    [TestCase("Aspose")]
    [TestCase("Syncfusion")]
    public async Task AddGotenbergWord_Renders_Templates_Through_A_Registered_Backend(string backend)
    {
        // a backend with a document model renders what Word.Mini cannot — headers, here
        var handler = new StubHandler(_ => Pdf());
        var services = new ServiceCollection();
        if (backend == "Aspose")
        {
            services.AddSingleton(new Regira.Office.Word.Aspose.AsposeWordConfig { AllowEvaluation = true });
            services.AddTransient<IWordCreator, Regira.Office.Word.Aspose.WordService>();
        }
        else
        {
            services.AddSingleton(new Regira.Office.Word.Syncfusion.SyncfusionWordConfig());
            services.AddTransient<IWordCreator, Regira.Office.Word.Syncfusion.WordService>();
        }
        services.AddGotenbergWord(o => o.BaseUrl = "http://gotenberg.test:3000");
        UseHandler(services, handler);
        await using var provider = services.BuildServiceProvider();

        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "A title" };
        input.Headers!.Add(new WordHeaderFooterInput { Template = TemplateInput("add_header.docx") });
        using var _ = await provider.GetRequiredService<IWordConverter>().Convert(input, FileFormat.Pdf);

        Assert.That(handler.Requests.Single().Uploads.Single().FileName, Is.EqualTo("document.docx"));
    }

    [Test]
    public async Task AddGotenbergWord_Resolves_Without_The_Optional_Services()
    {
        var services = new ServiceCollection();
        services.AddGotenbergWord(o => o.BaseUrl = "http://gotenberg.test:3000");
        using var provider = services.BuildServiceProvider();

        var toImages = provider.GetRequiredService<IWordToImagesService>();

        // resolved with the constructor's defaults: no rasteriser was registered
        await Assert.ThrowsAsync<InvalidOperationException>(() => toImages.ToImages(TemplateInput("template.docx")));
    }

    [Test]
    public async Task AddGotenbergWord_Takes_The_Optional_Services_From_The_Container()
    {
        var handler = new StubHandler(_ => Pdf());
        var rasteriser = new StubPdfToImageService();
        var creator = new StubCreator(ReadAsset("lorem_ipsum.docx").GetBytes()!);
        var services = new ServiceCollection();
        services.AddSingleton<IPdfToImageService>(rasteriser);
        services.AddSingleton<IWordCreator>(creator);
        services.AddGotenbergWord(o => o.BaseUrl = "http://gotenberg.test:3000");
        UseHandler(services, handler);
        await using var provider = services.BuildServiceProvider();

        var input = TemplateInput("parameters.docx");
        input.GlobalParameters = new Dictionary<string, object> { ["title"] = "A title" };
        var images = await provider.GetRequiredService<IWordToImagesService>().ToImages(input);

        Assert.Multiple(() =>
        {
            Assert.That(images.Count(), Is.EqualTo(2));
            Assert.That(creator.Inputs, Has.Count.EqualTo(1));
            Assert.That(rasteriser.Pdfs, Has.Count.EqualTo(1));
        });
    }

    [Test]
    public void AddGotenbergWord_Requires_A_BaseUrl()
    {
        var services = new ServiceCollection();

        Assert.Throws<ArgumentException>(() => services.AddGotenbergWord(_ => { }));
    }


    private byte[] Apply(string filename, DocumentSettings settings)
        => OpenXmlPageSetup.Apply(ReadAsset(filename).GetBytes()!, new ConversionOptions { OutputFormat = FileFormat.Pdf, Settings = settings });

    private static WordprocessingDocument Open(byte[] bytes)
        => WordprocessingDocument.Open(new MemoryStream(bytes), false);

    private static List<(uint Width, uint Height)> PageSizes(byte[] docx)
    {
        using var doc = Open(docx);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.PageSize>()
            .Select(size => (size.Width!.Value, size.Height!.Value))
            .ToList();
    }

    private static List<(long Cx, long Cy)> PictureExtents(byte[] docx)
    {
        using var doc = Open(docx);
        var body = doc.MainDocumentPart!.Document!.Body!;
        return body.Descendants<Wp.Extent>().Select(e => (e.Cx!.Value, e.Cy!.Value))
            .Concat(body.Descendants<A.Extents>().Select(e => (e.Cx!.Value, e.Cy!.Value)))
            .ToList();
    }

    /// <summary>The document's one group of shapes: its wp:extent, its own a:xfrm, and the a:xfrm of each shape in it.</summary>
    private static GroupLayout Group(byte[] docx)
    {
        using var doc = Open(docx);
        var body = doc.MainDocumentPart!.Document!.Body!;
        var extent = body.Descendants<Wp.Extent>().Single();
        var group = body.Descendants<A.TransformGroup>().Single();
        return new GroupLayout(
            (extent.Cx!.Value, extent.Cy!.Value),
            (group.Extents!.Cx!.Value, group.Extents.Cy!.Value),
            (group.ChildOffset!.X!.Value, group.ChildOffset.Y!.Value, group.ChildExtents!.Cx!.Value, group.ChildExtents.Cy!.Value),
            body.Descendants<A.Transform2D>()
                .Select(shape => (shape.Offset!.X!.Value, shape.Offset.Y!.Value, shape.Extents!.Cx!.Value, shape.Extents.Cy!.Value))
                .ToList());
    }

    /// <summary>An inline text box of 200 × 60 pt holding an inline picture of 100 × 50 pt: a drawing in a drawing.</summary>
    private static readonly string TextBoxHoldingAPicture = TextBoxHolding(200, 60, 100, 50);

    /// <summary>An inline text box holding an inline picture, each of the given size in points.</summary>
    private static string TextBoxHolding(double boxWidth, double boxHeight, double pictureWidth, double pictureHeight) => $"""
        <w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                   xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                   xmlns:pic="http://schemas.openxmlformats.org/drawingml/2006/picture"
                   xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
          <wp:inline distT="0" distB="0" distL="0" distR="0">
            <wp:extent cx="{Emus(boxWidth)}" cy="{Emus(boxHeight)}"/>
            <wp:docPr id="1" name="Text Box 1"/>
            <a:graphic>
              <a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingShape">
                <wps:wsp>
                  <wps:cNvSpPr txBox="1"/>
                  <wps:spPr>
                    <a:xfrm><a:off x="0" y="0"/><a:ext cx="{Emus(boxWidth)}" cy="{Emus(boxHeight)}"/></a:xfrm>
                    <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                  </wps:spPr>
                  <wps:txbx>
                    <w:txbxContent>
                      <w:p>
                        <w:r>
                          <w:drawing>
                            <wp:inline distT="0" distB="0" distL="0" distR="0">
                              <wp:extent cx="{Emus(pictureWidth)}" cy="{Emus(pictureHeight)}"/>
                              <wp:docPr id="2" name="Picture 2"/>
                              <a:graphic>
                                <a:graphicData uri="http://schemas.openxmlformats.org/drawingml/2006/picture">
                                  <pic:pic>
                                    <pic:nvPicPr><pic:cNvPr id="2" name="Picture 2"/><pic:cNvPicPr/></pic:nvPicPr>
                                    <pic:blipFill><a:blip/><a:stretch><a:fillRect/></a:stretch></pic:blipFill>
                                    <pic:spPr>
                                      <a:xfrm><a:off x="0" y="0"/><a:ext cx="{Emus(pictureWidth)}" cy="{Emus(pictureHeight)}"/></a:xfrm>
                                      <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                                    </pic:spPr>
                                  </pic:pic>
                                </a:graphicData>
                              </a:graphic>
                            </wp:inline>
                          </w:drawing>
                        </w:r>
                      </w:p>
                    </w:txbxContent>
                  </wps:txbx>
                  <wps:bodyPr/>
                </wps:wsp>
              </a:graphicData>
            </a:graphic>
          </wp:inline>
        </w:drawing>
        """;

    private static long Emus(double points) => (long)Math.Round(points * 12700);

    /// <summary>
    /// An inline drawing canvas of 300 × 200 pt holding two rectangles: 100 × 50 pt at (10, 10) and 80 × 60 pt at
    /// (150, 100).
    /// </summary>
    private const string CanvasOfTwoShapes = """
        <w:drawing xmlns:w="http://schemas.openxmlformats.org/wordprocessingml/2006/main"
                   xmlns:wp="http://schemas.openxmlformats.org/drawingml/2006/wordprocessingDrawing"
                   xmlns:a="http://schemas.openxmlformats.org/drawingml/2006/main"
                   xmlns:wps="http://schemas.microsoft.com/office/word/2010/wordprocessingShape"
                   xmlns:wpc="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas">
          <wp:inline distT="0" distB="0" distL="0" distR="0">
            <wp:extent cx="3810000" cy="2540000"/>
            <wp:docPr id="1" name="Canvas 1"/>
            <a:graphic>
              <a:graphicData uri="http://schemas.microsoft.com/office/word/2010/wordprocessingCanvas">
                <wpc:wpc>
                  <wpc:bg/>
                  <wpc:whole/>
                  <wps:wsp>
                    <wps:cNvPr id="2" name="Rectangle 2"/>
                    <wps:cNvSpPr/>
                    <wps:spPr>
                      <a:xfrm><a:off x="127000" y="127000"/><a:ext cx="1270000" cy="635000"/></a:xfrm>
                      <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                    </wps:spPr>
                    <wps:bodyPr/>
                  </wps:wsp>
                  <wps:wsp>
                    <wps:cNvPr id="3" name="Rectangle 3"/>
                    <wps:cNvSpPr/>
                    <wps:spPr>
                      <a:xfrm><a:off x="1905000" y="1270000"/><a:ext cx="1016000" cy="762000"/></a:xfrm>
                      <a:prstGeom prst="rect"><a:avLst/></a:prstGeom>
                    </wps:spPr>
                    <wps:bodyPr/>
                  </wps:wsp>
                </wpc:wpc>
              </a:graphicData>
            </a:graphic>
          </wp:inline>
        </w:drawing>
        """;

    /// <summary>The offset and size of each shape's <c>a:xfrm</c> in the body, in EMUs.</summary>
    private static List<(long X, long Y, long Cx, long Cy)> ShapeTransforms(byte[] docx)
    {
        using var doc = Open(docx);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<A.Transform2D>()
            .Select(xfrm => (xfrm.Offset!.X!.Value, xfrm.Offset.Y!.Value, xfrm.Extents!.Cx!.Value, xfrm.Extents.Cy!.Value))
            .ToList();
    }

    private static List<double> GridColumns(byte[] docx)
    {
        using var doc = Open(docx);
        return doc.MainDocumentPart!.Document!.Body!.Descendants<W.GridColumn>()
            .Select(column => double.Parse(column.Width!.Value!, System.Globalization.CultureInfo.InvariantCulture))
            .ToList();
    }

    private static HttpResponseMessage Pdf()
        => new(HttpStatusCode.OK) { Content = new ByteArrayContent(PdfBytes) };

    private static void UseHandler(IServiceCollection services, HttpMessageHandler handler)
        => services.ConfigureAll<HttpClientFactoryOptions>(options =>
            options.HttpMessageHandlerBuilderActions.Add(builder => builder.PrimaryHandler = handler));


    private sealed record GroupLayout((long Cx, long Cy) Extent, (long Cx, long Cy) Extents, (long X, long Y, long Cx, long Cy) ChildSpace,
        List<(long X, long Y, long Cx, long Cy)> Shapes);

    private sealed record Upload(string? Name, string? FileName, byte[] Bytes);

    private sealed record RecordedRequest(HttpMethod Method, Uri Uri, string? Authorization, List<Upload> Uploads);

    /// <summary>
    /// Records every request, including the multipart parts, and answers with <c>respond</c>. Without
    /// one, any request is unexpected.
    /// </summary>
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage>? respond = null) : HttpMessageHandler
    {
        public List<RecordedRequest> Requests { get; } = [];

        public HttpClient CreateClient() => new(this, false) { BaseAddress = new Uri("http://gotenberg.test/") };

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var uploads = new List<Upload>();
            if (request.Content is MultipartFormDataContent multipart)
            {
                foreach (var part in multipart)
                {
                    var disposition = part.Headers.ContentDisposition;
                    uploads.Add(new Upload(
                        disposition?.Name?.Trim('"'),
                        disposition?.FileNameStar ?? disposition?.FileName?.Trim('"'),
                        await part.ReadAsByteArrayAsync(cancellationToken)));
                }
            }
            Requests.Add(new RecordedRequest(request.Method, request.RequestUri!, request.Headers.Authorization?.ToString(), uploads));

            return respond?.Invoke(request)
                   ?? throw new InvalidOperationException("No request was expected.");
        }
    }

    private sealed class StubCreator(byte[] output) : IWordCreator
    {
        public List<WordTemplateInput> Inputs { get; } = [];

        public Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
        {
            Inputs.Add(input);
            return Task.FromResult(output.ToMemoryFile(ContentTypes.DOCX));
        }
    }

    private sealed class StubPdfToImageService : IPdfToImageService
    {
        public List<byte[]> Pdfs { get; } = [];
        public List<PdfToImagesOptions?> Options { get; } = [];

        public Task<IList<IImageFile>> ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default)
        {
            Pdfs.Add(pdf.GetBytes()!);
            Options.Add(options);
            IList<IImageFile> images = [new ImageFile { Bytes = [1] }, new ImageFile { Bytes = [2] }];
            return Task.FromResult(images);
        }
    }
}
