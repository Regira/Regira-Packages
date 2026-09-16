using System.Text.RegularExpressions;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;
using Regira.Office.Word.Syncfusion.Extensions;
using Regira.Utilities;
using Syncfusion.DocIO;
using Syncfusion.DocIO.DLS;
using Syncfusion.DocIORenderer;
using HeaderFooterType = Regira.Office.Word.Models.HeaderFooterType;
using Margins = Regira.Office.Models.Margins;
using RegiraFileFormat = Regira.Office.Models.FileFormat;
using RegiraPageOrientation = Regira.Office.Models.PageOrientation;
using RegiraPageSize = Regira.Office.Models.PageSize;
using RegiraParagraph = Regira.Office.Word.Models.Paragraph;

namespace Regira.Office.Word.Syncfusion;

/// <summary>
/// Full <see cref="IWordService"/> implementation on Syncfusion DocIO.
/// <para>
/// Two formats are out of reach and throw <see cref="NotSupportedException"/>: ODT cannot be
/// <em>loaded</em> (saving to ODT works), and EPUB export is unavailable on .NET Core.
/// </para>
/// </summary>
public class WordService : IWordService
{
    private const int MAX_DOCUMENT_INSERTS = 100;
    private static readonly Regex ParamRegex = new("{{ *[a-zA-Z0-9._]+ *}}");

    private int _insertDocumentCounter;

    public WordService(SyncfusionWordConfig? config = null)
    {
        // Must run before the first DocIO type is touched, or the output carries trial text.
        SyncfusionLicense.Register(config?.LicenseKey);
    }


    public Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        return Task.FromResult(ToMemoryFile(doc));
    }

    public async Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default)
    {
        using var doc = await MergeDocuments(inputs);
        return ToMemoryFile(doc);
    }

    public Task<IMemoryFile> Convert(WordTemplateInput input, RegiraFileFormat format, CancellationToken cancellationToken = default)
        => Convert(input, new ConversionOptions { OutputFormat = format }, cancellationToken);

    public Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        var converted = ConvertDocument(doc, options);
        // Unlike Word.Spire, the content type follows the actual output format.
        return Task.FromResult(converted.ToMemoryFile(GetContentType(options.OutputFormat)));
    }

    public Task<string> GetText(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        return Task.FromResult(doc.GetText());
    }

    public Task<IEnumerable<WordImage>> GetImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        var images = doc.FindAllPictures()
            .Select(pic => new WordImage
            {
                Name = pic.Title,
                Size = new ImageSize((int)pic.Width, (int)pic.Height),
                File = pic.ImageBytes.ToBinaryFile()
            })
            .ToList();
        return Task.FromResult<IEnumerable<WordImage>>(images);
    }

    public Task<IEnumerable<IImageFile>> ToImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        // The renderer is not passed anywhere: constructing it registers DocIO's rendering backend.
        using var renderer = new DocIORenderer();

        var images = doc.RenderAsImages()
            .Select(stream =>
            {
                using var page = stream;
                var buffer = new MemoryStream();
                page.Position = 0;
                page.CopyTo(buffer);
                var bytes = buffer.ToArray();
                var contentType = ContentTypeUtility.GetContentType(bytes);
                return (IImageFile)new ImageFile
                {
                    Bytes = bytes,
                    Length = bytes.Length,
                    ContentType = contentType,
                    Format = contentType.EndsWith("png", StringComparison.OrdinalIgnoreCase) ? ImageFormat.Png : ImageFormat.Jpeg
                };
            })
            .ToList();

        return Task.FromResult<IEnumerable<IImageFile>>(images);
    }


    protected internal IMemoryFile ToMemoryFile(WordDocument doc, FormatType format = FormatType.Docx)
        => doc.ToStream(format).ToMemoryFile(format == FormatType.Doc ? ContentTypes.DOC : ContentTypes.DOCX);

    protected internal async Task<WordDocument> MergeDocuments(IEnumerable<WordTemplateInput> inputs)
    {
        var doc = new WordDocument();

        WordDocument? firstDoc = null;
        foreach (var input in inputs.AsList())
        {
            using var newFile = await Create(input);
            await using var newStream = newFile.GetStream()!;

            var inputDoc = new WordDocument(newStream, FormatType.Docx);
            firstDoc ??= inputDoc;

            if (input.Options != null)
            {
                inputDoc = ProcessInputOptions(inputDoc, input.Options, firstDoc);
            }

            foreach (var section in inputDoc.Sections.OfType<WSection>())
            {
                // without this DocIO starts every imported section on a new page
                section.BreakCode = SectionBreakCode.NoBreak;
            }

            doc.ImportContent(inputDoc, ImportOptions.UseDestinationStyles);
        }

        return doc;
    }

    protected internal WordDocument CreateDocument(WordTemplateInput input, WordDocument? reference = null)
    {
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

        if (input.Headers?.Any() == true)
        {
            foreach (var inputHeader in input.Headers)
            {
                AddHeader(doc, CreateDocument(inputHeader.Template, reference), inputHeader.Type);
            }
        }
        if (input.Footers?.Any() == true)
        {
            foreach (var inputFooter in input.Footers)
            {
                AddFooter(doc, CreateDocument(inputFooter.Template, reference), inputFooter.Type);
            }
        }

        return ProcessInputOptions(doc, input.Options, reference);
    }

    protected internal WordDocument LoadDocument(IMemoryFile? template)
    {
        var doc = new WordDocument();

        using var stream = template?.GetStream();
        if (stream == null || stream == Stream.Null)
        {
            doc.EnsureMinimal();
            return doc;
        }

        try
        {
            doc.Open(stream, FormatType.Automatic);
        }
        catch (Exception ex) when (IsOpenDocumentText(stream))
        {
            doc.Dispose();
            throw new NotSupportedException(
                "DocIO cannot load ODT templates (saving to ODT is supported). Convert the template to .docx first.", ex);
        }

        return doc;
    }

    protected internal Stream ConvertDocument(WordDocument doc, ConversionOptions options)
    {
        if (options.Settings != null)
        {
            ApplyDocumentSettings(doc, options);
        }

        var format = options.OutputFormat;
        switch (format)
        {
            case RegiraFileFormat.Pdf:
            {
                using var renderer = new DocIORenderer();
                using var pdf = renderer.ConvertToPDF(doc);
                var ms = new MemoryStream();
                pdf.Save(ms);
                ms.Position = 0;
                return ms;
            }
            case RegiraFileFormat.Html:
                // Inline styles keep the result self-contained; DocIO offers no external-stylesheet
                // option, and a stylesheet file name has nowhere to go when saving to a stream.
                doc.SaveOptions.HtmlExportCssStyleSheetType = CssStyleSheetType.Inline;
                doc.SaveOptions.HtmlExportHeadersFooters = true;
                break;
            case RegiraFileFormat.EPub:
                throw new NotSupportedException(
                    "EPUB export is unavailable on .NET Core: DocIO supports it only on Windows Forms, WPF, UWP and ASP.NET Web/MVC.");
            case RegiraFileFormat.Png:
            case RegiraFileFormat.Jpeg:
                throw new NotSupportedException("Image output is not produced by Convert. Use ToImages instead.");
        }

        // FormatType shares its member names with FileFormat for every format handled here.
        return doc.ToStream(Enum.Parse<FormatType>(format.ToString(), true));
    }

    private void ApplyDocumentSettings(WordDocument doc, ConversionOptions options)
    {
        var settings = options.Settings!;

        foreach (var section in doc.Sections.OfType<WSection>())
        {
            var originalWidth = section.PageSetup.ClientWidth;

            var pageSize = GetPageSize(settings.PageSize);
            if (section.PageSetup.PageSize != pageSize)
            {
                section.PageSetup.PageSize = pageSize;
            }
            if (settings.Margins != null)
            {
                section.PageSetup.Margins = GetMargins(settings.Margins);
            }
            var orientation = GetPageOrientation(settings.PageOrientation);
            if (section.PageSetup.Orientation != orientation)
            {
                section.PageSetup.Orientation = orientation;
            }

            var scaleFactor = section.PageSetup.ClientWidth / originalWidth;

            if (options.AutoScaleTables)
            {
                foreach (var table in section.Body.FindAllTables())
                {
                    if (originalWidth / table.Width - 1 >= .1)
                    {
                        // narrower than the page: leave it at its designed width
                        continue;
                    }
                    // DocIO exposes no public auto-fit, so scale the cells the table width is made of
                    foreach (var cell in table.Rows.OfType<WTableRow>().SelectMany(row => row.Cells.OfType<WTableCell>()))
                    {
                        cell.Width *= scaleFactor;
                    }
                }
            }
            if (options.AutoScalePictures)
            {
                foreach (var picture in section.Body.FindAllPictures())
                {
                    picture.Width *= scaleFactor;
                    picture.Height *= scaleFactor;
                }
            }
        }
    }

    protected internal WordDocument ProcessInputOptions(WordDocument doc, InputOptions? options, WordDocument reference)
    {
        if (options?.RemoveEmptyParagraphs == true)
        {
            RemoveEmptyParagraphs(doc);
        }

        if (options?.HorizontalAlignment.HasValue == true || (options?.InheritFont == true && doc != reference))
        {
            var defaultStyle = reference.Styles.OfType<WParagraphStyle>()
                .FirstOrDefault(style => style.Name == "Normal");

            if (defaultStyle != null)
            {
                foreach (var paragraph in doc.FindAllParagraphs())
                {
                    var styleName = paragraph.StyleName;
                    if (string.IsNullOrEmpty(styleName) || styleName.StartsWith(defaultStyle.Name, StringComparison.OrdinalIgnoreCase))
                    {
                        if (options.InheritFont && doc != reference)
                        {
                            foreach (var textRange in paragraph.ChildEntities.OfType<WTextRange>())
                            {
                                textRange.CharacterFormat.FontName = defaultStyle.CharacterFormat.FontName;
                                textRange.CharacterFormat.FontSize = defaultStyle.CharacterFormat.FontSize;
                            }
                        }
                        if (options.HorizontalAlignment.HasValue)
                        {
                            paragraph.ParagraphFormat.HorizontalAlignment = options.HorizontalAlignment.Value.ToDocIO();
                        }
                    }
                }
            }
        }

        if (options?.EnforceEvenAmountOfPages == true && GetPageCount(doc) % 2 != 0)
        {
            var paragraph = (WParagraph)doc.LastSection.AddParagraph();
            paragraph.AppendBreak(BreakType.PageBreak);
        }

        return doc;
    }

    protected internal void AddParagraphs(WordDocument doc, IEnumerable<RegiraParagraph> paragraphs)
    {
        var section = doc.Sections.OfType<WSection>().FirstOrDefault() ?? doc.AddSection();
        foreach (var paragraph in paragraphs)
        {
            ((WParagraph)section.AddParagraph()).SetParagraph(paragraph);
        }
    }

    protected internal void AddHeader(WordDocument doc, WordDocument headerDoc, HeaderFooterType type)
    {
        var source = headerDoc.GetHeader(type);
        // fall back to the default header, then to the body
        if (type != HeaderFooterType.Default && source.IsEmpty())
        {
            source = headerDoc.GetHeader();
        }
        var content = source.ChildEntities.Count > 0 ? source.ChildEntities : headerDoc.Sections[0].Body.ChildEntities;

        doc.GetHeader(type).ReplaceChildEntities(content);

        if (type == HeaderFooterType.FirstPage)
        {
            doc.Sections[0].PageSetup.DifferentFirstPage = true;
        }
    }

    protected internal void AddFooter(WordDocument doc, WordDocument footerDoc, HeaderFooterType type)
    {
        var source = footerDoc.GetFooter(type);
        if (type != HeaderFooterType.Default && source.IsEmpty())
        {
            source = footerDoc.GetFooter();
        }
        var content = source.ChildEntities.Count > 0 ? source.ChildEntities : footerDoc.Sections[0].Body.ChildEntities;

        doc.GetFooter(type).ReplaceChildEntities(content);

        if (type == HeaderFooterType.FirstPage)
        {
            doc.Sections[0].PageSetup.DifferentFirstPage = true;
        }
    }

    protected internal void ReplaceGlobalParameters(WordDocument doc, IDictionary<string, object> parameters)
    {
        // BookmarkCollection is not IEnumerable
        var bookmarks = new List<Bookmark>();
        for (var i = 0; i < doc.Bookmarks.Count; i++)
        {
            bookmarks.Add(doc.Bookmarks[i]);
        }

        foreach (var parameter in parameters)
        {
            var parameterKey = parameter.Key;
            var parameterValue = parameter.Value.ToString() ?? string.Empty;

            var keyPattern = $"{{{{ *{parameterKey} *}}}}";
            if (parameterKey.StartsWith("html_", StringComparison.InvariantCultureIgnoreCase))
            {
                var selection = doc.Find(new Regex(keyPattern, RegexOptions.IgnoreCase));
                selection.GetAsOneRange().OwnerParagraph.InjectHtml(parameterValue);
            }
            else
            {
                // \v is Word's soft line break within a paragraph
                var replacementText = parameterValue.ReplaceLineEndings("\v");
                doc.Replace(new Regex(keyPattern, RegexOptions.IgnoreCase), replacementText);
            }

            var bookmark = bookmarks.FirstOrDefault(b => b.Name.Equals(parameterKey));
            if (bookmark != null)
            {
                var navigator = new BookmarksNavigator(doc);
                navigator.MoveToBookmark(bookmark.Name);
                navigator.ReplaceBookmarkContent(parameterValue, true);
                doc.Bookmarks.Remove(bookmark);
            }
        }
    }

    protected internal void ReplaceCollections(WordDocument doc, IDictionary<string, ICollection<IDictionary<string, object>>> collections)
    {
        foreach (var collectionEntry in collections)
        {
            var table = doc.FindTable(collectionEntry.Key);
            if (table == null)
            {
                return;
            }

            var data = collectionEntry.Value.ToList();
            var templateRow = table.Rows[1];
            table.Rows.RemoveAt(1);

            for (var r = 0; r < data.Count; r++)
            {
                var itemDic = DictionaryUtility.ToDictionary(data[r]);
                var newRow = templateRow.Clone();

                for (var i = 0; i < newRow.Cells.Count; i++)
                {
                    var cell = newRow.Cells[i];
                    var content = cell.Paragraphs.Count > 0 ? cell.Paragraphs[0].Text : string.Empty;
                    foreach (Match match in ParamRegex.Matches(content))
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
                        cell.Paragraphs[0].Replace(new Regex($"{{{{ *{key} *}}}}"), value?.ToString() ?? string.Empty);
                    }
                }

                table.Rows.Insert(1 + r, newRow);
            }
        }
    }

    protected internal void ReplaceImages(WordDocument doc, ICollection<WordImage> images)
    {
        foreach (var inputImage in images)
        {
            foreach (var templateImage in doc.FindAllPictures(inputImage.Name).ToArray())
            {
                var currentWidth = templateImage.Width;
                var currentHeight = templateImage.Height;

                var bytes = inputImage.File?.GetBytes();
                if (bytes == null)
                {
                    continue;
                }
                templateImage.LoadImage(bytes);
                // restore original width and height (overwritten by the new image's dimensions)
                templateImage.Width = currentWidth;
                templateImage.Height = currentHeight;
            }
        }
    }

    protected internal void InsertDocuments(WordDocument doc, IDictionary<string, WordTemplateInput> documentParameters, WordDocument? reference = null)
    {
        reference ??= doc;

        if (_insertDocumentCounter >= MAX_DOCUMENT_INSERTS)
        {
            // prevent infinite loops
            throw new InvalidOperationException("Maximum insertable documents reached");
        }
        _insertDocumentCounter++;

        var content = doc.GetText();

        foreach (var inputDocParameter in documentParameters)
        {
            var docKey = $"<{{ {inputDocParameter.Key} }}>";
            var regex = new Regex($"<{{ *{inputDocParameter.Key} *}}>");

            if (!regex.IsMatch(content))
            {
                continue;
            }

            // normalise white-space variations to one exact key
            doc.Replace(regex, docKey);

            using var otherDoc = CreateDocument(inputDocParameter.Value, reference);
            if (!otherDoc.HasEmptyBody())
            {
                InsertDocumentContent(doc, docKey, otherDoc);
            }
            else
            {
                if (regex.IsMatch(doc.GetHeaderText()))
                {
                    doc.GetHeader().ReplaceChildEntities(otherDoc.GetHeader().ChildEntities);
                }
                if (regex.IsMatch(doc.GetFooterText()))
                {
                    doc.GetFooter().ReplaceChildEntities(otherDoc.GetFooter().ChildEntities);
                }
            }
        }
    }

    /// <summary>
    /// Replaces every occurrence of <paramref name="docKey"/> with the body content of
    /// <paramref name="otherDoc"/> by cloning its child entities into the placeholder's
    /// container (body, header or footer).
    /// <para>
    /// This deliberately avoids DocIO's <c>Replace(string, IWordDocument, bool, bool)</c> overload.
    /// Cloning into the container is the behaviour the template contract describes, and it keeps the
    /// source document's style table out of the target — the same reason Word.Spire avoids its
    /// vendor's equivalent, where identically named styles (Normal, Header, Footer, ...) produce a
    /// circular BasedOn chain.
    /// </para>
    /// </summary>
    protected internal void InsertDocumentContent(WordDocument doc, string docKey, WordDocument otherDoc)
    {
        var sourceEntities = otherDoc.Sections.OfType<WSection>()
            .SelectMany(section => section.Body.ChildEntities.OfType<Entity>())
            .ToArray();

        foreach (var selection in doc.FindAll(new Regex(Regex.Escape(docKey))))
        {
            var placeholder = selection.GetAsOneRange().OwnerParagraph;
            var container = placeholder.OwnerTextBody;
            var index = container.ChildEntities.IndexOf(placeholder);
            if (index < 0)
            {
                continue;
            }

            // clone so the source entities stay attached to otherDoc (allows reuse across matches)
            for (var i = 0; i < sourceEntities.Length; i++)
            {
                container.ChildEntities.Insert(index + 1 + i, sourceEntities[i].Clone());
            }
            // drop the now-replaced placeholder paragraph
            container.ChildEntities.RemoveAt(index);
        }
    }

    protected internal void RemoveEmptyParagraphs(WordDocument doc)
    {
        foreach (var section in doc.Sections.OfType<WSection>())
        {
            for (var i = 0; i < section.Body.ChildEntities.Count; i++)
            {
                if (section.Body.ChildEntities[i] is WParagraph { } paragraph && paragraph.IsEmpty())
                {
                    section.Body.ChildEntities.RemoveAt(i);
                    i--;
                }
            }
        }
    }


    protected internal global::Syncfusion.Drawing.SizeF GetPageSize(RegiraPageSize size)
        => size switch
        {
            RegiraPageSize.A3 => PageSize.A3,
            RegiraPageSize.A5 => PageSize.A5,
            RegiraPageSize.A6 => PageSize.A6,
            _ => PageSize.A4
        };

    protected internal PageOrientation GetPageOrientation(RegiraPageOrientation orientation)
        => Enum.Parse<PageOrientation>(orientation.ToString());

    private static MarginsF GetMargins(Margins margins)
        => new()
        {
            Left = margins.Left,
            Top = margins.Top,
            Right = margins.Right,
            Bottom = margins.Bottom
        };

    /// <summary>
    /// DocIO exposes no page count on the document model, so this renders to PDF to count pages.
    /// </summary>
    private static int GetPageCount(WordDocument doc)
    {
        using var renderer = new DocIORenderer();
        using var pdf = renderer.ConvertToPDF(doc);
        return pdf.Pages.Count;
    }

    protected internal static string GetContentType(RegiraFileFormat format)
        => format switch
        {
            RegiraFileFormat.Pdf => ContentTypes.PDF,
            RegiraFileFormat.Html => ContentTypes.HTML,
            RegiraFileFormat.Doc or RegiraFileFormat.Dot => ContentTypes.DOC,
            RegiraFileFormat.Docx or RegiraFileFormat.Dotx or RegiraFileFormat.Docm or RegiraFileFormat.Dotm => ContentTypes.DOCX,
            _ => ContentTypeUtility.GetContentType($"x.{format.ToString().ToLowerInvariant()}")
        };

    private static bool IsOpenDocumentText(Stream stream)
    {
        // An .odt is a zip whose first entry is "mimetype", holding the ODF text media type
        // uncompressed - it sits around offset 38, so read comfortably past it.
        try
        {
            stream.Position = 0;
            var buffer = new byte[256];
            var read = stream.Read(buffer, 0, buffer.Length);
            return read > 0
                   && System.Text.Encoding.ASCII.GetString(buffer, 0, read).Contains("opendocument.text", StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }
}
