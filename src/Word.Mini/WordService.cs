using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using MiniSoftware;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;
using Regira.Media.Drawing.Dimensions;
using Regira.Office.MimeTypes;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Models;
using Regira.Office.Word.Templating;
using DrawingML = DocumentFormat.OpenXml.Drawing;
using W = DocumentFormat.OpenXml.Wordprocessing;
using Wp = DocumentFormat.OpenXml.Drawing.Wordprocessing;

namespace Regira.Office.Word.Mini;

/// <summary>
/// Creates Word documents from templates using MiniWord, and extracts text and images from the result.
/// <para>
/// Conversion (<see cref="IWordConverter"/>), merging (<see cref="IWordMerger"/>) and page rendering
/// (<see cref="IWordToImagesService"/>) are not available: MiniWord has no layout engine.
/// Use <c>Regira.Office.Word.Spire</c> for those.
/// </para>
/// </summary>
public class WordService : IWordCreator, IWordTextExtractor, IWordImageExtractor
{
    /// <summary>
    /// English Metric Units per pixel at 96 DPI - the unit OOXML uses for drawing extents.
    /// </summary>
    private const int EmusPerPixel = 9525;

    private static readonly Regex TagRegex = new(@"\{\{([^{}]*)\}\}", RegexOptions.Compiled);

    public async Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        EnsureSupported(input);

        var templateBytes = input.Template.GetBytes()
            ?? throw new ArgumentException("Template has no content.", nameof(input));

        var ms = new MemoryStream();
        await ms.SaveAsByTemplateAsync(TrimTags(ResolveConditions(templateBytes, input)), GetMiniValue(input), cancellationToken);
        // SaveAsByTemplateAsync leaves the stream at its end; rewind so consumers reading
        // Stream directly (rather than through GetStream()) see the content.
        ms.Position = 0;
        return ms.ToBinaryFile(ContentTypes.DOCX);
    }

    /// <summary>
    /// Extracts the text of the rendered document: body first, then headers, then footers.
    /// </summary>
    public async Task<string> GetText(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var stream = await Render(input, cancellationToken);
        using var doc = WordprocessingDocument.Open(stream, false);

        var sb = new StringBuilder();
        foreach (var (_, root) in ContentRoots(doc.MainDocumentPart))
        {
            AppendText(sb, root);
        }

        return sb.ToString();
    }

    public async Task<IEnumerable<WordImage>> GetImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        using var stream = await Render(input, cancellationToken);
        using var doc = WordprocessingDocument.Open(stream, false);

        var mainPart = doc.MainDocumentPart;
        if (mainPart == null)
        {
            return [];
        }

        var images = new List<WordImage>();
        var visited = new HashSet<string>();

        foreach (var (part, root) in ContentRoots(mainPart))
        {
            foreach (var drawing in root.Descendants<W.Drawing>())
            {
                var relationshipId = drawing.Descendants<DrawingML.Blip>().FirstOrDefault()?.Embed?.Value;
                if (string.IsNullOrEmpty(relationshipId) || part.GetPartById(relationshipId) is not ImagePart imagePart)
                {
                    continue;
                }

                visited.Add(imagePart.Uri.ToString());

                var properties = drawing.Descendants<Wp.DocProperties>().FirstOrDefault();
                var extent = drawing.Descendants<Wp.Extent>().FirstOrDefault();

                images.Add(new WordImage
                {
                    // A name the other backends find a picture by — its Alt-Text Title or Description — then the object name.
                    Name = FirstNotEmpty(properties?.Title?.Value, properties?.Description?.Value, properties?.Name?.Value)
                           ?? GetPartName(imagePart),
                    Size = extent == null
                        ? (ImageSize?)null
                        : new ImageSize((int)((extent.Cx?.Value ?? 0) / EmusPerPixel), (int)((extent.Cy?.Value ?? 0) / EmusPerPixel)),
                    File = ReadPart(imagePart)
                });
            }
        }

        // Images not reachable through a DrawingML drawing (legacy VML shapes, for example)
        // still have an image part - surface them, without a name or size.
        foreach (var imagePart in mainPart.ImageParts)
        {
            if (visited.Add(imagePart.Uri.ToString()))
            {
                images.Add(new WordImage
                {
                    Name = GetPartName(imagePart),
                    File = ReadPart(imagePart)
                });
            }
        }

        return images;
    }


    internal object GetMiniValue(WordTemplateInput input)
    {
        // GlobalParameters, Images and CollectionParameters all resolve against the same
        // {{tag}} namespace in the template, so a key may only appear in one of them.
        var values = new Dictionary<string, object>();

        void Add(string key, object value)
        {
            if (!values.TryAdd(key, value))
            {
                throw new ArgumentException(
                    $"Duplicate parameter key '{key}'. GlobalParameters, Images and CollectionParameters share a single template namespace.",
                    nameof(input));
            }
        }

        foreach (var parameter in input.GlobalParameters ?? new Dictionary<string, object>())
        {
            Add(parameter.Key, parameter.Value);
        }
        foreach (var image in input.Images ?? [])
        {
            Add(image.Name, ToPicture(image));
        }
        foreach (var collection in input.CollectionParameters ?? new Dictionary<string, ICollection<IDictionary<string, object>>>())
        {
            Add(collection.Key, collection.Value);
        }

        // the template's tags are trimmed (TrimTags), so a key spelled with spaces is reached by its trimmed form;
        // TryAdd, so a key the caller supplied itself is never displaced
        foreach (var spaced in values.Where(v => v.Key != v.Key.Trim()).ToArray())
        {
            values.TryAdd(spaced.Key.Trim(), spaced.Value);
        }

        return values;
    }

    /// <summary>
    /// Writes every tag in the template whole and without the spaces just inside its braces — <c>{{ title }}</c>
    /// becomes <c>{{title}}</c> and <c>{{ Items.Name }}</c> becomes <c>{{Items.Name}}</c> — in the body, headers
    /// and footers.
    /// <para>
    /// MiniWord matches a tag only when the text between the braces is the key itself, dotted collection and
    /// property tags included, and only when the tag sits in one run; a spaced tag, or one Word split over runs
    /// while it was edited, was left in the document as literal text, while Word.Spire, Word.Syncfusion and
    /// Word.Aspose match <c>{{ *key *}}</c> across runs. A split tag moves into the run it starts in, taking that
    /// run's formatting. The spaces inside a tag (<c>{{if(a == b)}}</c>) are MiniWord's own syntax and stay.
    /// </para>
    /// </summary>
    internal static byte[] TrimTags(byte[] template)
    {
        using var stream = new MemoryStream();
        stream.Write(template, 0, template.Length);
        var changed = false;
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            foreach (var (_, root) in ContentRoots(doc.MainDocumentPart))
            {
                foreach (var paragraph in root.Descendants<W.Paragraph>())
                {
                    // a text box inside the paragraph holds paragraphs of its own, rewritten on their own turn
                    var texts = paragraph.Descendants<W.Text>()
                        .Where(text => text.Ancestors<W.Paragraph>().First() == paragraph)
                        .ToList();
                    changed |= TrimTags(texts);
                }
            }
        }

        // disposing an editable document writes it back to the stream
        return changed ? stream.ToArray() : template;
    }

    /// <summary>
    /// Rewrites the tags in one paragraph's text elements, read as one string so a tag split over runs is found.
    /// </summary>
    private static bool TrimTags(IReadOnlyList<W.Text> texts)
    {
        // the paragraph's text, and for each character the element it came from
        var combined = new StringBuilder();
        var owner = new List<int>();
        for (var e = 0; e < texts.Count; e++)
        {
            combined.Append(texts[e].Text);
            owner.AddRange(Enumerable.Repeat(e, texts[e].Text.Length));
        }

        // a rewritten tag: its first character becomes the whole tag, the rest of its characters go
        var rewrites = new Dictionary<int, string>();
        var dropped = new HashSet<int>();
        foreach (Match match in TagRegex.Matches(combined.ToString()))
        {
            var key = match.Groups[1].Value.Trim();
            var last = match.Index + match.Length - 1;
            var intact = key.Length == match.Groups[1].Length && owner[match.Index] == owner[last];
            if (intact || key.Length == 0)
            {
                continue;
            }
            rewrites[match.Index] = $"{{{{{key}}}}}";
            dropped.UnionWith(Enumerable.Range(match.Index + 1, match.Length - 1));
        }
        if (rewrites.Count == 0)
        {
            return false;
        }

        var rebuilt = texts.Select(_ => new StringBuilder()).ToArray();
        for (var i = 0; i < combined.Length; i++)
        {
            if (rewrites.TryGetValue(i, out var tag))
            {
                rebuilt[owner[i]].Append(tag);
            }
            else if (!dropped.Contains(i))
            {
                rebuilt[owner[i]].Append(combined[i]);
            }
        }
        for (var e = 0; e < texts.Count; e++)
        {
            var value = rebuilt[e].ToString();
            if (value != texts[e].Text)
            {
                texts[e].Text = value;
                // a run may now start or end with a space that belongs to the surrounding text
                texts[e].Space = SpaceProcessingModeValues.Preserve;
            }
        }

        return true;
    }

    /// <summary>
    /// Resolves the template's conditional blocks, as <see cref="ConditionalBlocks"/> describes them, before MiniWord sees
    /// it, so they read the same as on the other backends; MiniWord's own <c>@if</c> is left alone.
    /// </summary>
    internal static byte[] ResolveConditions(byte[] template, WordTemplateInput input)
    {
        // read-only first: an editable package is written again when it closes, changed or not
        if (!UsesBlocks(template))
        {
            return template;
        }

        using var stream = new MemoryStream();
        stream.Write(template, 0, template.Length);
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var containers = ContentRoots(doc.MainDocumentPart)
                .SelectMany(content => content.Root.Descendants<W.Paragraph>())
                .Where(paragraph => ConditionalBlocks.ContainsMarker(GetOwnText(paragraph)))
                .Select(paragraph => paragraph.Parent)
                .OfType<OpenXmlElement>()
                .Distinct()
                .ToArray();

            foreach (var container in containers)
            {
                var lastBlock = LastBlock(container);
                foreach (var segment in Segments(container))
                {
                    var texts = segment.Select(child => child is W.Paragraph paragraph ? GetOwnText(paragraph) : null).ToArray();
                    foreach (var index in ConditionalBlocks.Resolve(texts, input))
                    {
                        Remove(segment[index]);
                    }
                }
                // a container that ends as the template wrote it is left as it is
                if (LastBlock(container) != lastBlock)
                {
                    EndWithParagraph(container);
                }
            }
        }

        // disposing an editable document writes it back to the stream
        return stream.ToArray();
    }

    private static bool UsesBlocks(byte[] template)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(template, false), false);
        return ContentRoots(doc.MainDocumentPart)
            .SelectMany(content => content.Root.Descendants<W.Paragraph>())
            .Any(paragraph => ConditionalBlocks.OpensBlock(GetOwnText(paragraph)));
    }

    /// <summary>
    /// The container's block content — paragraphs, tables, content controls — cut into sections: a paragraph carrying
    /// section properties ends its section, and so does a content control holding one. A block cannot span a section
    /// break, as on the other backends, where each section has a body of its own. Everything else among the children
    /// (cell properties, bookmark ends, the final section properties) is never part of a block and stays.
    /// </summary>
    private static IEnumerable<List<OpenXmlElement>> Segments(OpenXmlElement container)
    {
        var segment = new List<OpenXmlElement>();
        foreach (var child in container.ChildElements)
        {
            if (child is not (W.Paragraph or W.Table or W.SdtBlock))
            {
                continue;
            }
            segment.Add(child);
            if (child is W.Paragraph { ParagraphProperties.SectionProperties: not null }
                || child is W.SdtBlock control && control.Descendants<W.SectionProperties>().Any())
            {
                yield return segment;
                segment = [];
            }
        }
        yield return segment;
    }

    private static OpenXmlElement? LastBlock(OpenXmlElement container)
        => container.ChildElements.LastOrDefault(child => child is W.Paragraph or W.Table or W.SdtBlock);

    /// <summary>
    /// Removes a block's child. A paragraph carrying section properties keeps them and nothing else, so the section
    /// break stays and none of the paragraph's own formatting — numbering, a page break before it — is left behind
    /// on an empty line.
    /// </summary>
    private static void Remove(OpenXmlElement child)
    {
        if (child is W.Paragraph { ParagraphProperties: { SectionProperties: not null } properties } sectionEnd)
        {
            foreach (var content in sectionEnd.ChildElements.Where(c => c != properties).ToArray())
            {
                content.Remove();
            }
            foreach (var property in properties.ChildElements.Where(c => c is not W.SectionProperties).ToArray())
            {
                property.Remove();
            }
            return;
        }
        child.Remove();
    }

    /// <summary>
    /// Whatever holds paragraphs ends with one — a body, cell, header, footer, text box or content control; Word
    /// refuses a cell or text box without one — so one is added when the resolved blocks leave the container empty or
    /// ending in a table or a content control.
    /// </summary>
    private static void EndWithParagraph(OpenXmlElement container)
    {
        var last = LastBlock(container);
        if (last is W.Paragraph)
        {
            return;
        }
        if (last != null)
        {
            last.InsertAfterSelf(new W.Paragraph());
        }
        else if (container.GetFirstChild<W.SectionProperties>() is { } sectionProperties)
        {
            sectionProperties.InsertBeforeSelf(new W.Paragraph());
        }
        else
        {
            container.AppendChild(new W.Paragraph());
        }
    }

    /// <summary>
    /// The paragraph's text, without that of a text box inside it, whose paragraphs are read on their own.
    /// </summary>
    private static string GetOwnText(W.Paragraph paragraph)
        => string.Concat(paragraph.Descendants<W.Text>()
            .Where(text => text.Ancestors<W.Paragraph>().First() == paragraph)
            .Select(text => text.Text));

    /// <summary>
    /// Rejects the parts of <see cref="WordTemplateInput"/> MiniWord cannot honour, rather than
    /// dropping them silently and returning a document that is quietly missing content.
    /// </summary>
    private static void EnsureSupported(WordTemplateInput input)
    {
        var unsupported = new List<string>();

        if (input.DocumentParameters?.Count > 0)
        {
            unsupported.Add(nameof(input.DocumentParameters));
        }
        if (input.Headers?.Count > 0)
        {
            unsupported.Add(nameof(input.Headers));
        }
        if (input.Footers?.Count > 0)
        {
            unsupported.Add(nameof(input.Footers));
        }
        if (input.Options is { } options &&
            (options.InheritFont || options.HorizontalAlignment.HasValue || options.EnforceEvenAmountOfPages || options.RemoveEmptyParagraphs))
        {
            unsupported.Add(nameof(input.Options));
        }

        if (unsupported.Count > 0)
        {
            throw new NotSupportedException(
                $"MiniWord does not support {string.Join(", ", unsupported)}. Use Regira.Office.Word.Spire for these.");
        }
    }

    private static MiniWordPicture ToPicture(WordImage image)
    {
        var bytes = image.File?.GetBytes()
            ?? throw new ArgumentException($"Image '{image.Name}' has no content.", nameof(image));

        var picture = new MiniWordPicture
        {
            Bytes = bytes,
            // MiniWord selects the image part type from the extension; without it the picture
            // is written with the wrong content type and Word refuses to render it.
            Extension = GetImageExtension(image.File, bytes)
        };

        // MiniWordPicture defaults to 400x400. Only override when a size was actually supplied -
        // writing 0 would render the picture at zero width and height.
        if (image.Size is { Width: > 0, Height: > 0 } size)
        {
            picture.Width = size.Width;
            picture.Height = size.Height;
        }

        return picture;
    }

    private static string GetImageExtension(IMemoryFile file, byte[] bytes)
    {
        var fileName = (file as INamedFile)?.FileName;
        if (!string.IsNullOrWhiteSpace(fileName))
        {
            var extension = Path.GetExtension(fileName).TrimStart('.');
            if (extension.Length > 0)
            {
                return extension.ToLowerInvariant();
            }
        }

        var contentType = string.IsNullOrWhiteSpace(file.ContentType)
            ? ContentTypeUtility.GetContentType(bytes, fileName)
            : file.ContentType!;

        return ContentTypeUtility.GetExtension(contentType) ?? "png";
    }

    /// <summary>
    /// Text and image extraction run against the rendered document - matching Word.Spire, which
    /// also substitutes parameters before extracting.
    /// </summary>
    private async Task<Stream> Render(WordTemplateInput input, CancellationToken cancellationToken)
    {
        var file = await Create(input, cancellationToken);
        return file.GetStream() ?? new MemoryStream(file.GetBytes() ?? []);
    }

    private static IEnumerable<(OpenXmlPart Part, OpenXmlElement Root)> ContentRoots(MainDocumentPart? mainPart)
    {
        if (mainPart == null)
        {
            yield break;
        }

        if (mainPart.Document.Body is { } body)
        {
            yield return (mainPart, body);
        }
        foreach (var header in mainPart.HeaderParts)
        {
            yield return (header, header.Header);
        }
        foreach (var footer in mainPart.FooterParts)
        {
            yield return (footer, footer.Footer);
        }
    }

    private static void AppendText(StringBuilder sb, OpenXmlElement root)
    {
        foreach (var paragraph in root.Descendants<W.Paragraph>())
        {
            foreach (var element in paragraph.Descendants())
            {
                switch (element)
                {
                    case W.Text text:
                        sb.Append(text.Text);
                        break;
                    case W.TabChar:
                        sb.Append('\t');
                        break;
                    case W.Break:
                    case W.CarriageReturn:
                        sb.Append('\n');
                        break;
                }
            }
            sb.Append('\n');
        }
    }

    private static IBinaryFile ReadPart(ImagePart part)
    {
        using var source = part.GetStream();
        var buffer = new MemoryStream();
        source.CopyTo(buffer);
        return buffer.ToArray().ToBinaryFile(part.ContentType);
    }

    private static string GetPartName(ImagePart part) => Path.GetFileName(part.Uri.ToString());

    private static string? FirstNotEmpty(params string?[] values)
        => values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x));
}

[Obsolete("Use WordService instead.", false)]
public class WordCreator : WordService;
