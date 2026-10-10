using System.Text.RegularExpressions;
using Aspose.Words;
using Aspose.Words.Drawing;
using Aspose.Words.Fields;
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
using Regira.Office.Word.Layout;
using Regira.Office.Word.Models;
using Regira.Office.Word.Templating;
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

    public Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default)
        => Merge(inputs, null, cancellationToken);

    public async Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, MergeOptions? options, CancellationToken cancellationToken = default)
    {
        var doc = await MergeDocuments(inputs, options);
        return ToMemoryFile(doc);
    }

    public Task<IMemoryFile> Convert(WordTemplateInput input, RegiraFileFormat format, CancellationToken cancellationToken = default)
        => Convert(input, new ConversionOptions { OutputFormat = format }, cancellationToken);

    public Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default)
    {
        var doc = CreateDocument(input);
        var converted = ConvertDocument(doc, options);
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

    protected internal Task<Document> MergeDocuments(IEnumerable<WordTemplateInput> inputs)
        => MergeDocuments(inputs, null);
    protected internal Task<Document> MergeDocuments(IEnumerable<WordTemplateInput> inputs, MergeOptions? mergeOptions)
    {
        // The first document is the destination, so later ones take on its styles.
        Document? doc = null;
        var previousIsPadded = false;

        foreach (var input in inputs.AsList())
        {
            // its options processed once, InheritFont taking the destination's font before the padding counts the
            // pages, so they are counted in the font the input ends up in
            var inputDoc = CreateDocument(input, null, input.Options?.InheritFont == true ? doc : null);

            if (doc == null)
            {
                doc = inputDoc;
                previousIsPadded = input.Options?.EnforceEvenAmountOfPages == true;
                continue;
            }

            var starts = inputDoc.Sections.OfType<Section>().Select(section => section.PageSetup.SectionStart).ToList();
            var joint = doc.Sections.Count;
            doc.AppendDocument(inputDoc, ImportFormatMode.UseDestinationStyles);

            // AppendDocument starts an appended section on a new page whatever the source said, so the section starts
            // are set afterwards: the input's own breaks as it had them, and the joint as the merge asks
            for (var i = 0; i < starts.Count && joint + i < doc.Sections.Count; i++)
            {
                doc.Sections[joint + i].PageSetup.SectionStart = i == 0
                    ? JointStart(mergeOptions, previousIsPadded, input.Options?.EnforceEvenAmountOfPages == true)
                    : starts[i];
            }
            previousIsPadded = input.Options?.EnforceEvenAmountOfPages == true;
        }

        return Task.FromResult(doc ?? NewDocument());
    }

    private static SectionStart JointStart(MergeOptions? options, bool previousIsPadded, bool isPadded)
        => MergeJoints.Of(options, previousIsPadded, isPadded) switch
        {
            MergeJoint.OddPage => SectionStart.OddPage,
            MergeJoint.NewPage => SectionStart.NewPage,
            _ => SectionStart.Continuous
        };

    protected internal Document CreateDocument(WordTemplateInput input, Document? reference = null)
        => CreateDocument(input, reference, null);
    /// <summary>
    /// Builds the input. Its headers and footers take their font from <paramref name="reference"/>, the input itself from
    /// <paramref name="fontReference"/> where given: a merge's destination (<see cref="InputOptions.InheritFont"/>).
    /// </summary>
    private Document CreateDocument(WordTemplateInput input, Document? reference, Document? fontReference)
    {
        // nested documents, headers and footers all build through here
        using var nesting = NestedDocumentGuard.Enter();

        var doc = LoadDocument(input.Template);
        reference ??= doc;

        // first, so a dropped branch's placeholders are never filled or inserted
        ResolveBlocks(doc, input);

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

        return ProcessInputOptions(doc, input.Options, fontReference ?? reference);
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
            return NewDocument();
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

            var newWidth = GetClientWidth(section.PageSetup);

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
                    // Aspose throws for a shape past Word's 22-inch limit, so the factor stops there
                    var factor = PictureScaling.Factor(originalWidth, newWidth, picture.Width, picture.Height);
                    if (factor == 1)
                    {
                        // left as it is: setting even its own size throws for a picture already past the limit
                        continue;
                    }
                    // set with the aspect ratio unlocked: a locked shape recalculates the other side from each, which
                    // can take the side held at the limit a rounding error past it
                    var (width, height) = PictureScaling.Size(picture.Width, picture.Height, factor);
                    var locked = picture.AspectRatioLocked;
                    picture.AspectRatioLocked = false;
                    picture.Width = width;
                    picture.Height = height;
                    picture.AspectRatioLocked = locked;
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

    /// <summary>
    /// Resolves the document's template blocks, as <see cref="TemplateBlocks"/> describes them, and fills the fields of
    /// its loops' rows.
    /// </summary>
    protected internal void ResolveBlocks(Document doc, WordTemplateInput input)
        => new AsposeTemplateWalk(doc).Run(input);
    [Obsolete("Use ResolveBlocks, which resolves loop blocks as well.", false)]
    protected internal void ResolveConditions(Document doc, WordTemplateInput input)
        => ResolveBlocks(doc, input);

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
    /// A blank document: one section with one empty paragraph, on an A4 portrait page like the other Word backends'
    /// blank documents. Aspose's own blank document is US Letter.
    /// </summary>
    protected internal Document NewDocument()
    {
        var doc = new Document();
        SetPageSetup(doc.FirstSection.PageSetup, RegiraPageSize.A4, RegiraPageOrientation.Portrait);
        return doc;
    }

    /// <summary>
    /// Sets the page's size and orientation. The size is written as a width and height, because Aspose's
    /// paper-size list stops at A3–A5; every <see cref="RegiraPageSize"/> is honoured.
    /// </summary>
    protected internal void SetPageSetup(PageSetup pageSetup, RegiraPageSize size, RegiraPageOrientation orientation)
    {
        var (width, height) = WordPageSizes.Points(size);
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
    internal static string Literal(string replacement)
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
        => WordContentTypes.Of(format);
}
