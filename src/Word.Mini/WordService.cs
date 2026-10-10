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
using Regira.Office.Word.Mini.Internal;
using Regira.Office.Word.Packaging;
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

    /// <summary>
    /// A key MiniWord can be given as it is: letters, digits and underscores, its own tag grammar, none of which a
    /// regular expression reads as syntax.
    /// </summary>
    private static readonly Regex MiniWordKey = new(@"^\w+\z", RegexOptions.Compiled);

    public async Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        EnsureSupported(input);

        var templateBytes = input.Template.GetBytes()
            ?? throw new ArgumentException("Template has no content.", nameof(input));

        var (values, names, collections) = GetMiniInput(input);
        var ms = new MemoryStream();
        await ms.SaveAsByTemplateAsync(RewriteTags(ResolveBlocks(templateBytes, input), names, collections), values, cancellationToken);
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


    /// <summary>
    /// The values MiniWord fills the template with, and for each tag the name MiniWord knows its value by.
    /// <para>
    /// MiniWord builds a regular expression from every key it is given, a collection's <c>Items.Name</c> included:
    /// a key holding <c>[</c> throws, and one holding <c>.</c> or <c>(</c> matches other tags than its own. So a key
    /// of letters, digits and underscores is given as it is, any other key under a generated name of that form, and
    /// <see cref="RewriteTags(byte[], IReadOnlyDictionary{string, string}, IReadOnlyDictionary{string, string})"/> writes that name into the template's tags.
    /// </para>
    /// </summary>
    /// <returns>
    /// The values, each tag's name, and each collection's name by the tag of its key, for the field tags the names do
    /// not hold as written (<see cref="FieldName"/>).
    /// </returns>
    internal static (Dictionary<string, object> Values, IReadOnlyDictionary<string, string> Names, IReadOnlyDictionary<string, string> Collections) GetMiniInput(WordTemplateInput input)
    {
        // GlobalParameters, Images and CollectionParameters all resolve against the same
        // {{tag}} namespace in the template, so a key may only appear in one of them.
        var supplied = new Dictionary<string, object>();
        var names = new Dictionary<string, string>();
        var collectionNames = new Dictionary<string, string>();

        ArgumentException Duplicate(string key) => new(
            $"Duplicate parameter key '{key}'. GlobalParameters, Images and CollectionParameters share a single template namespace, which holds a collection's fields as Collection.Field.",
            nameof(input));

        void Add(string key, object value)
        {
            if (!supplied.TryAdd(key, value))
            {
                throw Duplicate(key);
            }
        }

        // a collection's {{Items.Name}} tags are in the namespace too, so a global key "Items.Name" is a duplicate of its field
        void Claim(string tag, string name)
        {
            if (!names.TryAdd(tag, name))
            {
                throw Duplicate(tag);
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
        var collections = input.CollectionParameters ?? new Dictionary<string, ICollection<IDictionary<string, object>>>();
        foreach (var collection in collections)
        {
            Add(collection.Key, collection.Value);
        }

        var values = new Dictionary<string, object>();
        foreach (var (key, value) in ByTag(supplied))
        {
            var tag = key.Trim();
            var name = MiniWordName(tag);
            Claim(tag, name);

            if (collections.TryGetValue(key, out var rows) && rows != null)
            {
                var (namedRows, fields) = NameFields(rows);
                foreach (var field in fields)
                {
                    Claim($"{tag}.{field.Key}", $"{name}.{field.Value}");
                }
                collectionNames[tag] = name;
                values[name] = namedRows;
            }
            else
            {
                values[name] = value;
            }
        }

        return (values, names, collectionNames);
    }

    /// <summary>
    /// The name of a collection field for a tag in the collection's row that <paramref name="names"/> does not hold as
    /// it is written: one spaced around its dot takes the name of the field the rows supply, and a field no row supplies
    /// is named under the collection's MiniWord name. MiniWord then reads the row as the collection's — removed when
    /// there are no rows, a missing field left empty — as it does for a collection whose key it takes as it is; under
    /// the caller's spelling the tag stays in the document as text. Null for a tag that reads no collection.
    /// </summary>
    private static string? FieldName(string tag, IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, string> collections)
    {
        // the longest prefix first: a collection key may hold a dot itself
        for (var dot = tag.LastIndexOf('.'); dot > 0; dot = tag.LastIndexOf('.', dot - 1))
        {
            var collectionTag = tag[..dot].Trim();
            if (collections.TryGetValue(collectionTag, out var collection))
            {
                var field = tag[(dot + 1)..].Trim();
                return names.TryGetValue($"{collectionTag}.{field}", out var known) ? known : $"{collection}.{MiniWordName(field)}";
            }
        }
        return null;
    }

    /// <summary>
    /// A collection's rows keyed by the names MiniWord is given for their fields, and for each field's tag that name.
    /// </summary>
    private static (List<Dictionary<string, object>> Rows, Dictionary<string, string> Fields) NameFields(IEnumerable<IDictionary<string, object>> rows)
    {
        var fields = new Dictionary<string, string>();
        var namedRows = new List<Dictionary<string, object>>();
        foreach (var row in rows)
        {
            var namedRow = new Dictionary<string, object>();
            foreach (var (key, value) in ByTag(row))
            {
                var tag = key.Trim();
                if (!fields.TryGetValue(tag, out var name))
                {
                    fields[tag] = name = MiniWordName(tag);
                }
                namedRow[name] = value;
            }
            namedRows.Add(namedRow);
        }
        return (namedRows, fields);
    }

    /// <summary>
    /// The entries by the tag that reads them — the key trimmed, as <see cref="RewriteTags(byte[], IReadOnlyDictionary{string, string}, IReadOnlyDictionary{string, string})"/> trims the template's tags.
    /// Of two keys reading the same tag, the one the caller spelled as the tag wins, so a key spelled with spaces never
    /// displaces one supplied without them.
    /// </summary>
    private static IEnumerable<KeyValuePair<string, T>> ByTag<T>(IEnumerable<KeyValuePair<string, T>> entries)
        => entries
            .GroupBy(entry => entry.Key.Trim())
            .Select(group => group.OrderBy(entry => entry.Key == group.Key ? 0 : 1).First());

    /// <summary>
    /// The name MiniWord is given for the key a tag reads: the tag itself when <see cref="MiniWordKey"/> allows it,
    /// otherwise a generated one.
    /// </summary>
    private static string MiniWordName(string tag)
        => MiniWordKey.IsMatch(tag) ? tag : $"regira_{Guid.NewGuid():N}";

    /// <summary>
    /// Writes every tag in the template whole, without the spaces just inside its braces, and under the name MiniWord
    /// knows its key by (<see cref="GetMiniInput"/>) — <c>{{ title }}</c> becomes <c>{{title}}</c>,
    /// <c>{{ Items.Name }}</c> becomes <c>{{Items.Name}}</c>, and <c>{{ Total (EUR) }}</c> takes the generated name of
    /// the key <c>Total (EUR)</c> — in the body, headers and footers.
    /// <para>
    /// MiniWord matches a tag only when the text between the braces is the key itself, dotted collection and
    /// property tags included, and only when the tag sits in one run; a spaced tag, or one Word split over runs
    /// while it was edited, was left in the document as literal text, while Word.Spire, Word.Syncfusion and
    /// Word.Aspose match <c>{{ *key *}}</c> across runs. A split tag moves into the run it starts in, taking that
    /// run's formatting. The spaces inside a tag (<c>{{if(a == b)}}</c>) are MiniWord's own syntax and stay.
    /// </para>
    /// </summary>
    internal static byte[] RewriteTags(byte[] template, IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, string> collections)
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
                    changed |= RewriteTags(MiniTemplateWalk.OwnTexts(paragraph).ToList(), names, collections);
                }
            }
        }

        // disposing an editable document writes it back to the stream
        return changed ? stream.ToArray() : template;
    }

    /// <summary>
    /// Rewrites the tags in one paragraph's text elements, read as one string so a tag split over runs is found.
    /// </summary>
    private static bool RewriteTags(IReadOnlyList<W.Text> texts, IReadOnlyDictionary<string, string> names, IReadOnlyDictionary<string, string> collections)
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
            if (key.Length == 0)
            {
                continue;
            }
            var name = names.TryGetValue(key, out var known) ? known : FieldName(key, names, collections) ?? key;
            var last = match.Index + match.Length - 1;
            var intact = name == match.Groups[1].Value && owner[match.Index] == owner[last];
            if (intact)
            {
                continue;
            }
            rewrites[match.Index] = $"{{{{{name}}}}}";
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
    /// Resolves the template's blocks, as <see cref="TemplateBlocks"/> describes them, and fills the fields of its loops'
    /// rows before MiniWord sees it, so they read the same as on the other backends; MiniWord's own <c>@if</c> and
    /// <c>@foreach</c> are left alone. Every tag is joined into one <c>w:t</c> first, as
    /// <see cref="RewriteTags(byte[], IReadOnlyDictionary{string, string}, IReadOnlyDictionary{string, string})"/>
    /// writes it, but under the key the template spells, so a row fills its field before MiniWord's names are written.
    /// </summary>
    internal static byte[] ResolveBlocks(byte[] template, WordTemplateInput input)
    {
        // read-only first: an editable package is written again when it closes, changed or not
        if (!UsesBlocks(template))
        {
            return template;
        }

        var keys = new Dictionary<string, string>();
        using var stream = new MemoryStream();
        stream.Write(template, 0, template.Length);
        using (var doc = WordprocessingDocument.Open(stream, true))
        {
            var roots = ContentRoots(doc.MainDocumentPart).Select(content => content.Root).ToArray();
            foreach (var paragraph in roots.SelectMany(root => root.Descendants<W.Paragraph>()))
            {
                RewriteTags(MiniTemplateWalk.OwnTexts(paragraph).ToList(), keys, keys);
            }
            new MiniTemplateWalk(doc, roots).Run(input);
        }
        // a copy's VML shapes — a text box's fallback, an embedded object — keep their original's ids
        VmlShapeIds.MakeUnique(stream);

        // disposing an editable document writes it back to the stream
        return stream.ToArray();
    }

    private static bool UsesBlocks(byte[] template)
    {
        using var doc = WordprocessingDocument.Open(new MemoryStream(template, false), false);
        return ContentRoots(doc.MainDocumentPart)
            .SelectMany(content => content.Root.Descendants<W.Paragraph>())
            .Any(paragraph => TemplateBlocks.OpensBlock(MiniTemplateWalk.GetOwnText(paragraph)));
    }

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
