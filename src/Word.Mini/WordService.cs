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
        await ms.SaveAsByTemplateAsync(templateBytes, GetMiniValue(input), cancellationToken);
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
                    // Matches how Word.Spire matches images: Alt-Text Title first, then Alt-Text Description.
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

        AddSpacedTagAliases(values, input.Template);

        return values;
    }

    /// <summary>
    /// MiniWord looks a tag up by the text between the braces <em>verbatim</em> — it does not trim.
    /// A template written <c>{{ title }}</c> therefore never matches the key <c>title</c>, while
    /// Word.Spire and Word.Syncfusion match on <c>{{ *key *}}</c> and replace it. Registering the
    /// spellings the template actually uses closes that gap without changing MiniWord.
    /// </summary>
    private static void AddSpacedTagAliases(IDictionary<string, object> values, IMemoryFile? template)
    {
        foreach (var tag in FindTemplateTags(template))
        {
            var key = tag.Trim();
            if (tag != key && values.TryGetValue(key, out var value))
            {
                // TryAdd: a template may spell the same key several ways, and an alias must never
                // displace a key the caller supplied itself.
                values.TryAdd(tag, value);
            }
        }
    }

    private static HashSet<string> FindTemplateTags(IMemoryFile? template)
    {
        var tags = new HashSet<string>(StringComparer.Ordinal);

        var bytes = template?.GetBytes();
        if (bytes == null)
        {
            return tags;
        }

        using var stream = new MemoryStream(bytes);
        using var doc = WordprocessingDocument.Open(stream, false);

        var sb = new StringBuilder();
        foreach (var (_, root) in ContentRoots(doc.MainDocumentPart))
        {
            AppendText(sb, root);
        }

        foreach (Match match in TagRegex.Matches(sb.ToString()))
        {
            tags.Add(match.Groups[1].Value);
        }

        return tags;
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
