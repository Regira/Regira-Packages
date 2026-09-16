using System.Text.RegularExpressions;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Replacing;
using Aspose.Words.Saving;
using Aspose.Words.Tables;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Aspose.Extensions;
using Regira.Office.Word.Aspose.Internal;
using Regira.Office.Word.Models;
using Regira.Utilities;
using AsposeDocumentBuilder = Aspose.Words.DocumentBuilder;
using AsposeParagraph = Aspose.Words.Paragraph;
using HeaderFooterType = Regira.Office.Word.Models.HeaderFooterType;
using ImageFormat = Regira.Media.Drawing.Enums.ImageFormat;
using ImageSize = Regira.Media.Drawing.Dimensions.ImageSize;
using RegiraFileFormat = Regira.Office.Models.FileFormat;
using RegiraPageOrientation = Regira.Office.Models.PageOrientation;
using RegiraPageSize = Regira.Office.Models.PageSize;
using RegiraParagraph = Regira.Office.Word.Models.Paragraph;

namespace Regira.Office.Word.Aspose;

/// <summary>
/// Full <see cref="IWordService"/> implementation on Aspose.Words. It loads every Word template format,
/// ODT included, and <c>Convert</c> writes every document <see cref="RegiraFileFormat"/>, EPUB included;
/// page images come from <see cref="ToImages"/>.
/// <para>
/// Aspose.Words is commercial. Without a licence (<see cref="AsposeWordConfig"/>) it runs in evaluation
/// mode, which watermarks every document and truncates long ones.
/// </para>
/// </summary>
public class WordService : IWordService
{
    private static readonly Regex ParamRegex = new("{{ *[a-zA-Z0-9._]+ *}}");

    public WordService(AsposeWordConfig? config = null)
    {
        // Must run before any other Aspose.Words type is used, or the output is evaluation output.
        AsposeLicense.Register(config);
    }


    public Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        var doc = CreateDocument(input);
        return Task.FromResult(ToMemoryFile(doc));
    }

    public async Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default)
    {
        var doc = await MergeDocuments(inputs);
        return ToMemoryFile(doc);
    }

    public Task<IMemoryFile> Convert(WordTemplateInput input, RegiraFileFormat format, CancellationToken cancellationToken = default)
        => Convert(input, new ConversionOptions { OutputFormat = format }, cancellationToken);

    public Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default)
    {
        var doc = CreateDocument(input);
        var converted = ConvertDocument(doc, options);
        // Unlike Word.Spire, the content type follows the actual output format.
        return Task.FromResult(converted.ToMemoryFile(GetContentType(options.OutputFormat)));
    }

    /// <summary>
    /// The document's plain text: the body, followed by every header and footer.
    /// </summary>
    public Task<string> GetText(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        var doc = CreateDocument(input);
        // Node.GetText would keep Word's control characters (cell ends, field codes); the text export drops them.
        var text = doc.ToString(new TxtSaveOptions { ExportHeadersFootersMode = TxtExportHeadersFootersMode.AllAtEnd });
        return Task.FromResult(text);
    }

    public Task<IEnumerable<WordImage>> GetImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        var doc = CreateDocument(input);
        var images = doc.FindAllPictures()
            // a linked picture has no bytes in the document
            .Where(pic => !pic.ImageData.IsLinkOnly)
            .Select(pic => new WordImage
            {
                Name = pic.Title,
                Size = new ImageSize((int)pic.Width, (int)pic.Height),
                File = pic.ImageData.ToByteArray().ToBinaryFile()
            })
            .ToList();
        return Task.FromResult<IEnumerable<WordImage>>(images);
    }

    public Task<IEnumerable<IImageFile>> ToImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        var doc = CreateDocument(input);
        doc.UpdatePageLayout();

        var images = new List<IImageFile>(doc.PageCount);
        for (var page = 0; page < doc.PageCount; page++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            using var ms = new MemoryStream();
            doc.Save(ms, new ImageSaveOptions(SaveFormat.Jpeg) { PageSet = new PageSet(page) });
            var bytes = ms.ToArray();
            images.Add(new ImageFile
            {
                Bytes = bytes,
                Length = bytes.Length,
                ContentType = ContentTypeUtility.GetContentType(bytes),
                Format = ImageFormat.Jpeg
            });
        }

        return Task.FromResult<IEnumerable<IImageFile>>(images);
    }


    protected internal IMemoryFile ToMemoryFile(Document doc, SaveFormat format = SaveFormat.Docx)
        => doc.ToStream(format).ToMemoryFile(format == SaveFormat.Doc ? ContentTypes.DOC : ContentTypes.DOCX);

    protected internal async Task<Document> MergeDocuments(IEnumerable<WordTemplateInput> inputs)
    {
        // The first document is the destination, so later ones take on its styles.
        Document? doc = null;

        foreach (var input in inputs.AsList())
        {
            using var newFile = await Create(input);
            using var newStream = newFile.GetStream()!;

            var inputDoc = new Document(newStream);
            if (input.Options != null)
            {
                inputDoc = ProcessInputOptions(inputDoc, input.Options, doc ?? inputDoc);
            }

            if (doc == null)
            {
                doc = inputDoc;
                continue;
            }

            foreach (var section in inputDoc.Sections.OfType<Section>())
            {
                // without this every appended document starts on a new page
                section.PageSetup.SectionStart = SectionStart.Continuous;
            }
            doc.AppendDocument(inputDoc, ImportFormatMode.UseDestinationStyles);
        }

        return doc ?? new Document();
    }

    protected internal Document CreateDocument(WordTemplateInput input, Document? reference = null)
    {
        // nested documents, headers and footers all build through here
        using var nesting = NestedDocumentGuard.Enter();

        var doc = LoadDocument(input.Template);
        reference ??= doc;

        if (input.DocumentParameters?.Any() == true)
        {
            InsertDocuments(doc, input.DocumentParameters);
        }
        // collection parameters first so the globalParameters don't interfere
        if (input.CollectionParameters?.Any() == true)
        {
            ReplaceCollections(doc, input.CollectionParameters);
        }
        if (input.Images?.Any() == true)
        {
            ReplaceImages(doc, input.Images);
        }
        if (input.GlobalParameters?.Any() == true)
        {
            ReplaceGlobalParameters(doc, input.GlobalParameters);
        }

        if (input.Headers?.Any() == true || input.Footers?.Any() == true)
        {
            var pageSetup = doc.FirstSection.PageSetup;
            var hadFirstPage = pageSetup.DifferentFirstPageHeaderFooter;
            var hadEvenPages = pageSetup.OddAndEvenPagesHeaderFooter;

            foreach (var inputHeader in input.Headers ?? [])
            {
                AddHeader(doc, CreateDocument(inputHeader.Template, reference), inputHeader.Type);
            }
            foreach (var inputFooter in input.Footers ?? [])
            {
                AddFooter(doc, CreateDocument(inputFooter.Template, reference), inputFooter.Type);
            }

            FillSwitchedOnStories(doc,
                !hadFirstPage && pageSetup.DifferentFirstPageHeaderFooter,
                !hadEvenPages && pageSetup.OddAndEvenPagesHeaderFooter);
        }

        return ProcessInputOptions(doc, input.Options, reference);
    }

    /// <summary>
    /// A first-page or even-page header switches those pages to stories of their own — footers included — so the
    /// footer the input left alone would vanish from them, and the other way round. Where adding the input switched
    /// such stories on, an empty one takes the default story's content.
    /// </summary>
    private static void FillSwitchedOnStories(Document doc, bool firstPage, bool evenPages)
    {
        var switchedOn = new[] { (firstPage, HeaderFooterType.FirstPage), (evenPages, HeaderFooterType.Even) };
        foreach (var (_, type) in switchedOn.Where(x => x.Item1))
        {
            FillWhenEmpty(doc.GetHeader(type), doc.GetHeader());
            FillWhenEmpty(doc.GetFooter(type), doc.GetFooter());
        }
    }

    private static void FillWhenEmpty(HeaderFooter target, HeaderFooter source)
    {
        if (target.IsEmpty() && !source.IsEmpty())
        {
            target.ReplaceChildNodes(source);
        }
    }

    protected internal Document LoadDocument(IMemoryFile? template)
    {
        using var stream = template?.GetStream();
        if (stream == null || stream == Stream.Null)
        {
            // a blank document: one section with one empty paragraph
            return new Document();
        }

        // the format, ODT included, is detected from the content
        return new Document(stream);
    }

    protected internal Stream ConvertDocument(Document doc, ConversionOptions options)
    {
        if (options.Settings != null)
        {
            ApplyDocumentSettings(doc, options);
        }

        if (options.OutputFormat == RegiraFileFormat.Html)
        {
            return doc.ToStream(new HtmlSaveOptions(SaveFormat.Html)
            {
                // a stream has no folder next to it for images or a stylesheet
                ExportImagesAsBase64 = true,
                CssStyleSheetType = CssStyleSheetType.Embedded,
                ExportHeadersFootersMode = ExportHeadersFootersMode.PerSection
            });
        }

        return doc.ToStream(GetSaveFormat(options.OutputFormat));
    }

    private void ApplyDocumentSettings(Document doc, ConversionOptions options)
    {
        var settings = options.Settings!;

        foreach (var section in doc.Sections.OfType<Section>())
        {
            var originalWidth = GetClientWidth(section.PageSetup);

            SetPageSetup(section.PageSetup, settings.PageSize, settings.PageOrientation);
            if (settings.Margins != null)
            {
                section.PageSetup.LeftMargin = settings.Margins.Left;
                section.PageSetup.TopMargin = settings.Margins.Top;
                section.PageSetup.RightMargin = settings.Margins.Right;
                section.PageSetup.BottomMargin = settings.Margins.Bottom;
            }

            // a page without text width has nothing to scale against
            var scaleFactor = originalWidth > 0 ? GetClientWidth(section.PageSetup) / originalWidth : 1;

            if (options.AutoScaleTables)
            {
                foreach (var table in section.Body.FindAllTables().ToArray())
                {
                    var tableWidth = GetWidth(table);
                    if (tableWidth <= 0 || originalWidth / tableWidth - 1 >= .1)
                    {
                        // narrower than the page: leave it at its designed width
                        continue;
                    }
                    table.AutoFit(AutoFitBehavior.AutoFitToWindow);
                }
            }
            if (options.AutoScalePictures)
            {
                foreach (var picture in section.Body.FindAllPictures().ToArray())
                {
                    var (width, height) = (picture.Width, picture.Height);
                    picture.Width = width * scaleFactor;
                    picture.Height = height * scaleFactor;
                }
            }
        }
    }

    protected internal Document ProcessInputOptions(Document doc, InputOptions? options, Document reference)
    {
        if (options?.RemoveEmptyParagraphs == true)
        {
            RemoveEmptyParagraphs(doc);
        }

        if (options?.HorizontalAlignment.HasValue == true || (options?.InheritFont == true && doc != reference))
        {
            var defaultStyle = reference.Styles[StyleIdentifier.Normal];

            foreach (var paragraph in doc.FindAllParagraphs().ToArray())
            {
                var styleName = paragraph.ParagraphFormat.StyleName;
                if (string.IsNullOrEmpty(styleName) || styleName.StartsWith(defaultStyle.Name, StringComparison.OrdinalIgnoreCase))
                {
                    if (options.InheritFont && doc != reference)
                    {
                        // a style object cannot move between documents, so its font is copied onto the runs
                        foreach (var run in paragraph.Runs.OfType<Run>())
                        {
                            run.Font.Name = defaultStyle.Font.Name;
                            run.Font.Size = defaultStyle.Font.Size;
                        }
                    }
                    if (options.HorizontalAlignment.HasValue)
                    {
                        paragraph.ParagraphFormat.Alignment = options.HorizontalAlignment.Value.ToAspose();
                    }
                }
            }
        }

        if (options?.EnforceEvenAmountOfPages == true)
        {
            doc.UpdatePageLayout();
            if (doc.PageCount % 2 != 0)
            {
                var builder = new AsposeDocumentBuilder(doc);
                builder.MoveToDocumentEnd();
                builder.InsertBreak(BreakType.PageBreak);
            }
        }

        return doc;
    }

    protected internal void AddParagraphs(Document doc, IEnumerable<RegiraParagraph> paragraphs)
    {
        if (doc.FirstSection == null)
        {
            doc.EnsureMinimum();
        }
        var body = doc.FirstSection!.Body;
        // a blank document's single empty paragraph would otherwise open the content
        var placeholder = body.Count == 1 && body.FirstParagraph is { HasChildNodes: false } empty ? empty : null;

        foreach (var paragraph in paragraphs)
        {
            body.AppendChild(new AsposeParagraph(doc)).SetParagraph(paragraph);
        }

        placeholder?.Remove();
    }

    protected internal void AddHeader(Document doc, Document headerDoc, HeaderFooterType type)
    {
        var source = headerDoc.GetHeader(type);
        // fall back to the default header, then to the body
        if (type != HeaderFooterType.Default && source.IsEmpty())
        {
            source = headerDoc.GetHeader();
        }
        IEnumerable<Node> content = source.HasChildNodes ? source : headerDoc.FirstSection.Body;

        doc.GetHeader(type).ReplaceChildNodes(content);

        if (type == HeaderFooterType.FirstPage)
        {
            doc.FirstSection.PageSetup.DifferentFirstPageHeaderFooter = true;
        }
        else if (type == HeaderFooterType.Even)
        {
            // even-page stories only render once the document tells odd and even pages apart
            doc.FirstSection.PageSetup.OddAndEvenPagesHeaderFooter = true;
        }
    }

    protected internal void AddFooter(Document doc, Document footerDoc, HeaderFooterType type)
    {
        var source = footerDoc.GetFooter(type);
        if (type != HeaderFooterType.Default && source.IsEmpty())
        {
            source = footerDoc.GetFooter();
        }
        IEnumerable<Node> content = source.HasChildNodes ? source : footerDoc.FirstSection.Body;

        doc.GetFooter(type).ReplaceChildNodes(content);

        if (type == HeaderFooterType.FirstPage)
        {
            doc.FirstSection.PageSetup.DifferentFirstPageHeaderFooter = true;
        }
        else if (type == HeaderFooterType.Even)
        {
            // even-page stories only render once the document tells odd and even pages apart
            doc.FirstSection.PageSetup.OddAndEvenPagesHeaderFooter = true;
        }
    }

    protected internal void ReplaceGlobalParameters(Document doc, IDictionary<string, object> parameters)
    {
        var bookmarks = doc.Range.Bookmarks.ToList();

        foreach (var parameter in parameters)
        {
            var parameterKey = parameter.Key;
            var parameterValue = parameter.Value?.ToString() ?? string.Empty;

            var keyPattern = new Regex($"{{{{ *{Regex.Escape(parameterKey)} *}}}}", RegexOptions.IgnoreCase);
            if (parameterKey.StartsWith("html_", StringComparison.InvariantCultureIgnoreCase))
            {
                doc.FindParagraphs(keyPattern).FirstOrDefault()?.InjectHtml(parameterValue);
            }
            else
            {
                // a line break within the paragraph, as Word's Shift+Enter
                var replacementText = Literal(parameterValue).ReplaceLineEndings(ControlChar.LineBreak);
                doc.Range.Replace(keyPattern, replacementText, new FindReplaceOptions());
            }

            var bookmark = bookmarks.FirstOrDefault(b => b.Name.Equals(parameterKey));
            if (bookmark != null)
            {
                bookmark.Text = parameterValue;
                // the bookmark goes, its text stays
                bookmark.Remove();
            }
        }
    }

    protected internal void ReplaceCollections(Document doc, IDictionary<string, ICollection<IDictionary<string, object>>> collections)
    {
        foreach (var collectionEntry in collections)
        {
            var table = doc.FindTable(collectionEntry.Key);
            if (table == null)
            {
                // a template without this table: the other collections still apply
                continue;
            }

            var data = collectionEntry.Value.ToList();
            var templateRow = table.Rows[1];
            table.Rows.RemoveAt(1);

            for (var r = 0; r < data.Count; r++)
            {
                var itemDic = DictionaryUtility.ToDictionary(data[r]);
                var newRow = (Row)templateRow.Clone(true);
                table.Rows.Insert(1 + r, newRow);

                foreach (var cell in newRow.Cells.OfType<Cell>())
                {
                    var paragraph = cell.FirstParagraph;
                    if (paragraph == null)
                    {
                        continue;
                    }

                    foreach (Match match in ParamRegex.Matches(paragraph.GetText()))
                    {
                        var key = match.Value.Trim("{ }".ToCharArray());
                        object? value;
                        switch (key)
                        {
                            case "row_number":
                                value = r + 1;
                                break;
                            default:
                                itemDic.TryGetValue(key, out value);
                                break;
                        }
                        paragraph.Range.Replace(new Regex($"{{{{ *{Regex.Escape(key)} *}}}}"), Literal(value?.ToString() ?? string.Empty));
                    }
                }
            }
        }
    }

    protected internal void ReplaceImages(Document doc, ICollection<WordImage> images)
    {
        foreach (var inputImage in images)
        {
            var bytes = inputImage.File?.GetBytes();
            if (bytes == null)
            {
                continue;
            }

            foreach (var templateImage in doc.FindAllPictures(inputImage.Name).ToArray())
            {
                var currentWidth = templateImage.Width;
                var currentHeight = templateImage.Height;

                using var stream = new MemoryStream(bytes);
                templateImage.ImageData.SetImage(stream);
                // keep the placeholder's size, whatever the new image's dimensions
                templateImage.Width = currentWidth;
                templateImage.Height = currentHeight;
            }
        }
    }

    protected internal void InsertDocuments(Document doc, IDictionary<string, WordTemplateInput> documentParameters, Document? reference = null)
    {
        reference ??= doc;

        var content = doc.GetText();

        foreach (var inputDocParameter in documentParameters)
        {
            var docKey = $"<{{ {inputDocParameter.Key} }}>";
            var regex = new Regex($"<{{ *{Regex.Escape(inputDocParameter.Key)} *}}>");

            if (!regex.IsMatch(content))
            {
                continue;
            }

            // normalise white-space variations to one exact key
            doc.Range.Replace(regex, Literal(docKey));

            var otherDoc = CreateDocument(inputDocParameter.Value, reference);
            if (!otherDoc.HasEmptyBody())
            {
                InsertDocumentContent(doc, docKey, otherDoc);
            }
            else
            {
                if (regex.IsMatch(doc.GetHeaderText()))
                {
                    doc.GetHeader().ReplaceChildNodes(otherDoc.GetHeader());
                }
                if (regex.IsMatch(doc.GetFooterText()))
                {
                    doc.GetFooter().ReplaceChildNodes(otherDoc.GetFooter());
                }
            }
        }
    }

    /// <summary>
    /// Replaces every paragraph holding <paramref name="docKey"/> with the body content of
    /// <paramref name="otherDoc"/>, imported into the placeholder's container (body, cell, header or
    /// footer).
    /// <para>
    /// Nodes belong to one document, so they are imported rather than cloned. Destination styles win,
    /// the way Word.Spire and Word.Syncfusion keep the source document's style table out of the target.
    /// </para>
    /// </summary>
    protected internal void InsertDocumentContent(Document doc, string docKey, Document otherDoc)
    {
        var sourceNodes = otherDoc.Sections.OfType<Section>()
            .SelectMany(section => section.Body.ToArray())
            .ToArray();
        var importer = new NodeImporter(otherDoc, doc, ImportFormatMode.UseDestinationStyles);

        foreach (var placeholder in doc.FindParagraphs(new Regex(Regex.Escape(docKey))))
        {
            var container = placeholder.ParentNode;
            if (container == null)
            {
                continue;
            }

            Node previous = placeholder;
            foreach (var node in sourceNodes)
            {
                previous = container.InsertAfter(importer.ImportNode(node, true), previous);
            }
            // drop the now-replaced placeholder paragraph
            placeholder.Remove();

            if (container.LastChild is Table)
            {
                // a cell, header or footer has to end with a paragraph
                container.AppendChild(new AsposeParagraph(doc));
            }
        }
    }

    protected internal void RemoveEmptyParagraphs(Document doc)
    {
        foreach (var section in doc.Sections.OfType<Section>())
        {
            foreach (var paragraph in section.Body.Paragraphs.OfType<AsposeParagraph>().Where(p => p.IsEmpty()).ToArray())
            {
                paragraph.Remove();
            }
        }
    }


    /// <summary>
    /// Sets the page's size and orientation. The size is written as a width and height, because Aspose's
    /// paper-size list stops at A3–A5; every <see cref="RegiraPageSize"/> is honoured.
    /// </summary>
    protected internal void SetPageSetup(PageSetup pageSetup, RegiraPageSize size, RegiraPageOrientation orientation)
    {
        var (width, height) = PageSizes.Points(size);
        var landscape = orientation == RegiraPageOrientation.Landscape;

        // orientation first: the explicit width and height below then hold whatever it does to them
        pageSetup.Orientation = landscape ? Orientation.Landscape : Orientation.Portrait;
        pageSetup.PageWidth = landscape ? height : width;
        pageSetup.PageHeight = landscape ? width : height;
    }

    /// <summary>
    /// Escapes a replacement text for <c>Range.Replace</c>, which reads <c>&amp;p</c>, <c>&amp;l</c>,
    /// <c>&amp;m</c> and <c>&amp;b</c> as breaks even when the pattern is a regular expression.
    /// </summary>
    private static string Literal(string replacement)
        => replacement.Replace("&", "&&");

    private static double GetClientWidth(PageSetup pageSetup)
        => pageSetup.PageWidth - pageSetup.LeftMargin - pageSetup.RightMargin;

    /// <summary>
    /// The table's width in points: its preferred width when that is absolute, otherwise the sum of its
    /// first row's cells.
    /// </summary>
    private static double GetWidth(Table table)
        => table.PreferredWidth.Type switch
        {
            PreferredWidthType.Points => table.PreferredWidth.Value,
            // a percentage already follows the page
            PreferredWidthType.Percent => 0,
            _ => table.FirstRow?.Cells.OfType<Cell>().Sum(cell => cell.CellFormat.Width) ?? 0
        };

    /// <summary>
    /// Maps every document format explicitly: Aspose's <see cref="SaveFormat"/> has dozens of members,
    /// and image output belongs to <see cref="ToImages"/>.
    /// </summary>
    protected internal static SaveFormat GetSaveFormat(RegiraFileFormat format)
        => format switch
        {
            RegiraFileFormat.Docx => SaveFormat.Docx,
            RegiraFileFormat.Doc => SaveFormat.Doc,
            RegiraFileFormat.Dotx => SaveFormat.Dotx,
            RegiraFileFormat.Dot => SaveFormat.Dot,
            RegiraFileFormat.Docm => SaveFormat.Docm,
            RegiraFileFormat.Dotm => SaveFormat.Dotm,
            RegiraFileFormat.Pdf => SaveFormat.Pdf,
            RegiraFileFormat.Html => SaveFormat.Html,
            RegiraFileFormat.Rtf => SaveFormat.Rtf,
            RegiraFileFormat.Odt => SaveFormat.Odt,
            RegiraFileFormat.EPub => SaveFormat.Epub,
            RegiraFileFormat.Png or RegiraFileFormat.Jpeg
                => throw new NotSupportedException("Image output is not produced by Convert. Use ToImages instead."),
            _ => throw new ArgumentOutOfRangeException(nameof(format), format, null)
        };

    protected internal static string GetContentType(RegiraFileFormat format)
        => format switch
        {
            RegiraFileFormat.Pdf => ContentTypes.PDF,
            RegiraFileFormat.Html => ContentTypes.HTML,
            RegiraFileFormat.Doc or RegiraFileFormat.Dot => ContentTypes.DOC,
            RegiraFileFormat.Docx or RegiraFileFormat.Dotx or RegiraFileFormat.Docm or RegiraFileFormat.Dotm => ContentTypes.DOCX,
            RegiraFileFormat.Odt => "application/vnd.oasis.opendocument.text",
            RegiraFileFormat.EPub => "application/epub+zip",
            _ => ContentTypeUtility.GetContentType($"x.{format.ToString().ToLowerInvariant()}")
        };
}
