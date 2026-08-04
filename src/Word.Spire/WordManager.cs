using Regira.Drawing.SkiaSharp.Utilities;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;
using Regira.Office.Word.Spire.Extensions;
using Regira.TreeList;
using Regira.Utilities;
using Spire.Doc;
using Spire.Doc.Documents;
using Spire.Doc.Fields;
using System.Drawing;
using System.Text.RegularExpressions;
using HeaderFooterType = Regira.Office.Word.Models.HeaderFooterType;
using Margins = Regira.Office.Models.Margins;
using RegiraFileFormat = Regira.Office.Models.FileFormat;
using RegiraHorizontalAlignment = Regira.Office.Word.Models.HorizontalAlignment;
using RegiraPageOrientation = Regira.Office.Models.PageOrientation;
using RegiraPageSize = Regira.Office.Models.PageSize;
using RegiraParagraph = Regira.Office.Word.Models.Paragraph;
using SpireFileFormat = Spire.Doc.FileFormat;
using SpireHorizontalAlignment = Spire.Doc.Documents.HorizontalAlignment;
using SpirePageOrientation = Spire.Doc.Documents.PageOrientation;
using SpirePageSize = Spire.Doc.Documents.PageSize;
using SpireParagraph = Spire.Doc.Documents.Paragraph;

namespace Regira.Office.Word.Spire;

public class WordManager : IWordService
{
    private const int MAX_DOCUMENT_INSERTS = 100;
    private int _insertDocumentCounter;
    private static readonly Regex ParamRegex = new("{{ *[a-zA-Z0-9._]+ *}}");

    public Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        var file = ToMemoryFile(doc);
        return Task.FromResult(file);
    }
    public async Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default)
    {
        using var doc = await MergeDocuments(inputs);
        return ToMemoryFile(doc);
    }
    public Task<IMemoryFile> Convert(WordTemplateInput input, RegiraFileFormat format, CancellationToken cancellationToken = default)
    {
        return Convert(input, new ConversionOptions { OutputFormat = format }, cancellationToken);
    }
    public Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        var convertedStream = ConvertDocument(doc, options);
        var file = convertedStream.ToMemoryFile(options.OutputFormat == RegiraFileFormat.Doc ? ContentTypes.DOC : ContentTypes.DOCX);
        return Task.FromResult(file);
    }

    public Task<string> GetText(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        var contents = doc.GetText();
        return Task.FromResult(contents);
    }
    public Task<IEnumerable<WordImage>> GetImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var doc = CreateDocument(input);
        var tree = doc.ToTreeList();
        var pictures = tree.FindAllPictures();
        var images = pictures.Select(pic => new WordImage
        {
            Name = pic.Title,
            Size = new ImageSize((int)pic.Width, (int)pic.Height),
            File = pic.ImageBytes.ToBinaryFile()
        }).ToList();
        return Task.FromResult<IEnumerable<WordImage>>(images);
    }
    public Task<IEnumerable<IImageFile>> ToImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        //throw new NotSupportedException("https://www.e-iceblue.com/forum/missingmethodexception-when-converting-document-to-images-t9466.html");
        using var doc = CreateDocument(input);
        var images = new List<IImageFile>();
        for (var i = 0; i < doc.PageCount; i++)
        {
            var img = doc.SaveToImages(i, ImageType.Bitmap);
            images.Add(img.ToImageFile(SkiaSharp.SKEncodedImageFormat.Jpeg));
        }
        return Task.FromResult<IEnumerable<IImageFile>>(images);
    }


    protected internal IMemoryFile ToMemoryFile(Document doc, SpireFileFormat format = SpireFileFormat.Docx)
        => doc.ToStream(format).ToMemoryFile(format == SpireFileFormat.Doc ? ContentTypes.DOC : ContentTypes.DOCX);
    protected internal async Task<Document> MergeDocuments(IEnumerable<WordTemplateInput> inputs)
    {
        var doc = new Document();

        var inputList = inputs.AsList();
        Document? firstDoc = null;
        foreach (var input in inputList)
        {
            using var newFile = await Create(input);
#if NET8_0_OR_GREATER
            await using var newStream = newFile.GetStream();
#else
            using var newStream = newFile.GetStream();
#endif
            var options = input.Options;
            if (options != null)
            {
                var inputDoc = new Document(newStream, SpireFileFormat.Auto);
                firstDoc ??= inputDoc;
                inputDoc = ProcessInputOptions(inputDoc, options, firstDoc);

#if NET8_0_OR_GREATER
                await using var processedDocStream = inputDoc.ToStream();
#else
                using var processedDocStream = inputDoc.ToStream();
#endif
                doc.InsertTextFromStream(processedDocStream, SpireFileFormat.Auto);
            }
            else
            {
                doc.InsertTextFromStream(newStream, SpireFileFormat.Auto);
            }
        }

        return doc;
    }
    protected internal Document CreateDocument(WordTemplateInput input, Document? reference = null)
    {
        var doc = new Document();
        reference ??= doc;

        using var templateStream = input.Template.GetStream();

        if (templateStream != null && templateStream != Stream.Null)
        {
            doc.LoadFromStream(templateStream, SpireFileFormat.Auto, XHTMLValidationType.None);
        }

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
    protected internal Stream ConvertDocument(Document doc, ConversionOptions options)
    {
        if (options.Settings != null)
        {
            var newSize = options.Settings.PageSize;
            var newOrientation = options.Settings.PageOrientation;
            var newMargins = options.Settings.Margins;

            var docTree = doc.ToTreeList();
            var sectionTreeItems = docTree.Roots.GetChildren();
            foreach (var sectionTreeItem in sectionTreeItems)
            {
                var section = (Section)sectionTreeItem.Value;
                var originalWidth = section.PageSetup.ClientWidth;

                // PageSize
                var spireSize = GetPageSize(newSize);
                if (spireSize != section.PageSetup.PageSize)
                {
                    section.PageSetup.PageSize = spireSize;
                }
                // Margins
                if (newMargins != null)
                {
                    section.PageSetup.Margins = GetMargins(newMargins);
                }
                // Orientation
                var spireOrientation = GetPageOrientation(newOrientation);
                if (spireOrientation != section.PageSetup.Orientation)
                {
                    section.PageSetup.Orientation = spireOrientation;
                }

                var newWidth = section.PageSetup.ClientWidth;
                var scaleFactor = newWidth / originalWidth;

                // adjust tables
                if (options.AutoScaleTables)
                {
                    var tables = sectionTreeItem.FindAllTables();
                    foreach (var table in tables)
                    {
                        if (originalWidth / table.Width - 1 < .1)
                        {
                            table.AutoFit(AutoFitBehaviorType.AutoFitToWindow);
                        }
                    }
                }
                // adjust pictures
                if (options.AutoScalePictures)
                {
                    var pictures = sectionTreeItem.FindAllPictures();
                    foreach (var picture in pictures)
                    {
                        picture.Width *= scaleFactor;
                        picture.Height *= scaleFactor;
                    }
                }
            }
        }

        var format = options.OutputFormat;
        switch (format)
        {
            case RegiraFileFormat.Html:
                doc.HtmlExportOptions.ImageEmbedded = true;
                // embed the CSS in the document; the default (External) requires a separate .css
                // file path, which isn't available when saving HTML to a stream
                doc.HtmlExportOptions.CssStyleSheetType = CssStyleSheetType.Internal;
                break;
            case RegiraFileFormat.Png:
            case RegiraFileFormat.Jpeg:
                throw new Exception("Not supported. Use function ToImages instead");
        }

        var spireFormat = (SpireFileFormat)Enum.Parse(typeof(SpireFileFormat), options.OutputFormat.ToString(), true);
        return doc.ToStream(spireFormat);
    }
    protected internal Document ProcessInputOptions(Document doc, InputOptions? options, Document reference)
    {
        if (options?.RemoveEmptyParagraphs == true)
        {
            RemoveEmptyParagraphs(doc);
        }

        if (options?.HorizontalAlignment.HasValue == true || (options?.InheritFont == true && doc != reference))
        {
            var docStyles = doc.Styles.Cast<Style>().ToArray();
            var defaultStyle = reference.Styles.Cast<Style>()
                .FirstOrDefault(style => style.Name == "Normal");

            if (defaultStyle != null)
            {
                var paragraphs = doc
                    .ToTreeList()
                    .FindAllParagraphs();

                foreach (var paragraph in paragraphs)
                {
                    var paragraphStyle = docStyles.FirstOrDefault(s => s.StyleId == paragraph.StyleName);
                    if (paragraphStyle?.Name.StartsWith(defaultStyle.Name) ?? true)
                    {
                        if (options.InheritFont && doc != reference)
                        {
                            paragraph.ApplyStyle(defaultStyle);
                        }

                        if (options.HorizontalAlignment.HasValue)
                        {
                            paragraph.Format.HorizontalAlignment = GetHorizontalAlignment(options.HorizontalAlignment.Value);
                        }
                    }
                }
            }
        }

        if (options?.EnforceEvenAmountOfPages == true)
        {
            if (doc.PageCount % 2 != 0)
            {
                var paragraph = new SpireParagraph(doc);
                paragraph.AppendBreak(BreakType.PageBreak);
                doc.LastSection.Paragraphs.Add(paragraph);
            }
        }

        return doc;
    }

    protected internal void AddParagraphs(Document doc, IEnumerable<RegiraParagraph> paragraphs)
    {
        var section = (Section?)doc.Sections.FirstItem ?? doc.AddSection();
        foreach (var paragraph in paragraphs)
        {
            var spireParagraph = section.AddParagraph();
            spireParagraph.SetSpireParagraph(paragraph);
        }
    }
    protected internal void AddHeader(Document doc, Document headerDoc, HeaderFooterType type)
    {
        // try find content in same type
        var srcHeader = headerDoc.GetHeader(type);
        // look for content in default type
        if (type != HeaderFooterType.Default && IsEmpty(srcHeader))
        {
            srcHeader = headerDoc.GetHeader();
        }
        // get content from header, or when empty from body
        var childObjects = srcHeader.ChildObjects.Count > 0
            ? srcHeader.ChildObjects
            : headerDoc.Sections[0].Body.ChildObjects;

        var docHeader = doc.GetHeader(type);
        docHeader.ReplaceChildObjects(childObjects);

        if (type == HeaderFooterType.FirstPage)
        {
            var section = doc.Sections[0];
            section.PageSetup.DifferentFirstPageHeaderFooter = true;
        }
    }
    protected internal void AddFooter(Document doc, Document footerDoc, HeaderFooterType type)
    {
        // try find content in same type
        var srcFooter = footerDoc.GetFooter(type);
        // look for content in default type
        if (type != HeaderFooterType.Default && IsEmpty(srcFooter))
        {
            srcFooter = footerDoc.GetFooter();
        }
        // get content from header, or when empty from body
        var childObjects = srcFooter.ChildObjects.Count > 0
            ? srcFooter.ChildObjects
            : footerDoc.Sections[0].Body.ChildObjects;

        var docFooter = doc.GetFooter(type);
        docFooter.ReplaceChildObjects(childObjects);

        if (type == HeaderFooterType.FirstPage)
        {
            var section = doc.Sections[0];
            section.PageSetup.DifferentFirstPageHeaderFooter = true;
        }
    }
    protected internal void ReplaceGlobalParameters(Document doc, IDictionary<string, object> parameters)
    {
        var bookmarks = doc.Bookmarks
            .Cast<Bookmark>()
            .ToList();
        foreach (var parameter in parameters)
        {
            var parameterKey = parameter.Key;
            var parameterValue = parameter.Value.ToString() ?? string.Empty;

            var keyPattern = $"{{{{ *{parameterKey} *}}}}";
            if (parameterKey.StartsWith("html_", StringComparison.InvariantCultureIgnoreCase))
            {
                var sel = doc.FindPattern(new Regex(keyPattern, RegexOptions.IgnoreCase));
                sel.GetAsOneRange().OwnerParagraph.InjectHtml(parameterValue);
            }
            else
            {
                var replacementText = parameterValue.ReplaceLineEndings("\v");
                doc.Replace(new Regex(keyPattern, RegexOptions.IgnoreCase), replacementText);
            }

            var bookmark = bookmarks.FirstOrDefault(b => b.Name.Equals(parameterKey));
            if (bookmark != null)
            {
                var target = bookmark.BookmarkStart.NextSibling;
                if (target is TextRange tr)
                {
                    tr.Text = parameterValue;
                }
                // remove replaced bookmark?
                doc.Bookmarks.Remove(bookmark);
            }
        }
    }
    protected internal void ReplaceCollections(Document doc, IDictionary<string, ICollection<IDictionary<string, object>>> collections)
    {
        foreach (var collectionEntry in collections)
        {
            var name = collectionEntry.Key;
            var data = collectionEntry.Value.ToList();

            var docTree = doc.ToTreeList();
            var table = docTree.FindTable(name);
            if (table == null)
            {
                return;
            }

            var templateRow = table.Rows[1];
            table.Rows.RemoveAt(1);

            for (var r = 0; r < data.Count; r++)
            {
                var item = data[r];
                var itemDic = DictionaryUtility.ToDictionary(item);

                var newRow = templateRow.Clone();
                for (var i = 0; i < newRow.Cells.Count; i++)
                {
                    var content = newRow.Cells[i].FirstParagraph.Text;
                    var matches = ParamRegex.Matches(content);
                    foreach (Match match in matches)
                    {
                        object? value;
                        var key = match.Value.Trim("{ }".ToCharArray());
                        switch (key)
                        {
                            case "row_number":
                                value = r + 1;
                                break;
                            default:
                                itemDic.TryGetValue(key, out value);
                                break;
                        }
                        newRow.Cells[i].FirstParagraph.Replace(new Regex($"{{{{ *{key} *}}}}"), value?.ToString());
                    }
                }

                table.Rows.Insert(1 + r, newRow);
            }
        }
    }
    protected internal void ReplaceImages(Document doc, ICollection<WordImage> images)
    {
        var docTree = doc.ToTreeList();
        foreach (var inputImage in images)
        {
            var templateImages = docTree.FindAllPictures(inputImage.Name);
            foreach (var templateImage in templateImages)
            {
                var currentWidth = templateImage.Width;
                var currentHeight = templateImage.Height;

                templateImage.LoadImage(inputImage.File?.GetBytes());
                // restore original width and height (overwritten by new image dimensions)
                templateImage.Width = currentWidth;
                templateImage.Height = currentHeight;
            }
        }
    }
    protected internal void InsertDocuments(Document doc, IDictionary<string, WordTemplateInput> documentParameters, Document? reference = null)
    {
        reference ??= doc;

        if (_insertDocumentCounter >= MAX_DOCUMENT_INSERTS)
        {
            // prevent infinite loops
            throw new Exception("Maximum insertable documents reached");
        }
        _insertDocumentCounter++;

        var content = doc.GetText();

        foreach (var inputDocParameter in documentParameters)
        {
            var docKey = $"<{{ {inputDocParameter.Key} }}>";
            var regex = new Regex($"<{{ *{inputDocParameter.Key} *}}>");

            if (regex.IsMatch(content))
            {
                // support white-space variations
                doc.Replace(regex, docKey);

                var otherDoc = CreateDocument(inputDocParameter.Value, reference);
                var otherTree = otherDoc.ToTreeList();
                if (!otherTree.HasEmptyBody())
                {
                    InsertDocumentContent(doc, docKey, otherDoc);
                }
                else
                {
                    var headerContent = doc.GetHeaderText();
                    var footerContent = doc.GetFooterText();
                    if (regex.IsMatch(headerContent))
                    {
                        var srcHeader = otherDoc.GetHeader();
                        var targetHeader = doc.GetHeader();
                        targetHeader.ReplaceChildObjects(srcHeader.ChildObjects);
                    }
                    if (regex.IsMatch(footerContent))
                    {
                        var srcFooter = otherDoc.GetFooter();
                        var targetFooter = doc.GetFooter();
                        targetFooter.ReplaceChildObjects(srcFooter.ChildObjects);
                    }
                }
            }
        }
    }
    /// <summary>
    /// Replaces every occurrence of <paramref name="docKey"/> with the body content of
    /// <paramref name="otherDoc"/> by cloning its child objects into the placeholder's
    /// container (body, header or footer).
    /// <para>
    /// This deliberately avoids Spire's <c>Document.Replace(string, Document, bool, bool)</c>
    /// overload: that overload imports the source document's style table, and when both
    /// documents contain identically named styles (Normal, Header, Footer, ...) Spire builds a
    /// circular BasedOn reference and recurses infinitely (StackOverflowException) while
    /// resolving those styles.
    /// </para>
    /// </summary>
    protected internal void InsertDocumentContent(Document doc, string docKey, Document otherDoc)
    {
        var sourceObjects = otherDoc.Sections.Cast<Section>()
            .SelectMany(section => section.Body.ChildObjects.Cast<DocumentObject>())
            .ToArray();

        foreach (var selection in doc.FindAllString(docKey, false, true))
        {
            var placeholder = selection.GetAsOneRange().OwnerParagraph;
            var container = placeholder.OwnerTextBody;
            var index = container.ChildObjects.IndexOf(placeholder);
            if (index < 0)
            {
                continue;
            }

            // clone so the source objects stay attached to otherDoc (allows reuse across matches)
            for (var i = 0; i < sourceObjects.Length; i++)
            {
                container.ChildObjects.Insert(index + 1 + i, sourceObjects[i].Clone());
            }
            // drop the now-replaced placeholder paragraph
            container.ChildObjects.RemoveAt(index);
        }
    }
    protected internal SizeF GetPageSize(RegiraPageSize size)
    {
        switch (size)
        {
            case RegiraPageSize.A3:
                return SpirePageSize.A3;
            case RegiraPageSize.A5:
                return SpirePageSize.A5;
            case RegiraPageSize.A6:
                return SpirePageSize.A6;
            default:
                return SpirePageSize.A4;
        }
    }
    private MarginsF GetMargins(Margins margins)
    {
        return new MarginsF(margins.Left, margins.Top, margins.Right, margins.Bottom);
    }
    protected internal SpirePageOrientation GetPageOrientation(RegiraPageOrientation orientation)
    {
        return (SpirePageOrientation)Enum.Parse(typeof(SpirePageOrientation), orientation.ToString());
    }
    protected internal SpireHorizontalAlignment GetHorizontalAlignment(RegiraHorizontalAlignment alignment)
    {
        return Enum.Parse<SpireHorizontalAlignment>(alignment.ToString());
    }
    protected internal void RemoveEmptyParagraphs(Document doc)
    {
        foreach (Section section in doc.Sections)
        {
            for (int i = 0; i < section.Body.ChildObjects.Count; i++)
            {
                if (section.Body.ChildObjects[i].DocumentObjectType == DocumentObjectType.Paragraph)
                {
                    var paragraph = (SpireParagraph)section.Body.ChildObjects[i];
                    if (paragraph.IsEmpty())
                    {
                        section.Body.ChildObjects.Remove(paragraph);
                        i--;
                    }
                }

            }
        }
    }

    protected internal bool IsEmpty(Body obj)
    {
        if (obj.Paragraphs.Count == 1)
        {
            return obj.Paragraphs[0].ChildObjects.Count == 0;
        }

        return obj.Paragraphs.Count == 0;
    }
}
