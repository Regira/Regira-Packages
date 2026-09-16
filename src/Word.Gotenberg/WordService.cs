using System.Net;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.MimeTypes;
using Regira.Office.PDF.Abstractions;
using Regira.Office.Word.Abstractions;
using Regira.Office.Word.Gotenberg.Internal;
using Regira.Office.Word.Models;
using RegiraFileFormat = Regira.Office.Models.FileFormat;

namespace Regira.Office.Word.Gotenberg;

/// <summary>
/// Converts Word documents to PDF, and renders their pages as images, through the LibreOffice route of
/// a <see href="https://gotenberg.dev">Gotenberg</see> server.
/// <para>
/// Gotenberg has no document model, so this backend implements only <see cref="IWordConverter"/> and
/// <see cref="IWordToImagesService"/>, and <c>Convert</c> produces PDF only. Two collaborators widen
/// what it accepts:
/// </para>
/// <list type="bullet">
/// <item>an <see cref="IWordCreator"/> (<c>Regira.Office.Word.Mini</c>, for example) renders an input
/// that carries template substitutions before it is converted — without one, such an input throws
/// <see cref="NotSupportedException"/>;</item>
/// <item>an <see cref="IPdfToImageService"/> (<c>Regira.Office.PDF.DocNET</c>, for example)
/// rasterises the PDF for <see cref="ToImages"/>, since Gotenberg has no route that does.</item>
/// </list>
/// <para>
/// <see cref="ConversionOptions.Settings"/> is written into the document's section properties before
/// upload, so it needs an OOXML (<c>.docx</c>) source.
/// </para>
/// </summary>
/// <param name="client">
/// An <see cref="HttpClient"/> whose <see cref="HttpClient.BaseAddress"/> points at the Gotenberg server
/// (with a trailing slash). <c>AddGotenbergWord</c> configures it from <see cref="GotenbergWordConfig"/>.
/// </param>
/// <param name="config">Optional settings; only <see cref="GotenbergWordConfig.ImageOptions"/> is read here.</param>
/// <param name="pdfToImages">Rasterises the converted PDF for <see cref="ToImages"/>.</param>
/// <param name="creator">Renders template substitutions before conversion.</param>
public class WordService(
    HttpClient client,
    GotenbergWordConfig? config = null,
    IPdfToImageService? pdfToImages = null,
    IWordCreator? creator = null) : IWordConverter, IWordToImagesService
{
    internal const string ConvertRoute = "forms/libreoffice/convert";

    public Task<IMemoryFile> Convert(WordTemplateInput input, RegiraFileFormat format, CancellationToken cancellationToken = default)
        => Convert(input, new ConversionOptions { OutputFormat = format }, cancellationToken);

    public async Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default)
    {
        EnsurePdf(options.OutputFormat);

        var (bytes, extension) = await GetSource(input, cancellationToken);

        if (options.Settings != null)
        {
            if (!SourceFormat.IsOpenXml(extension))
            {
                throw new NotSupportedException(
                    $"Page settings are written into the document before conversion, which needs an OOXML (.docx) source; this one is .{extension}.");
            }
            bytes = OpenXmlPageSetup.Apply(bytes, options);
        }

        var pdf = await PostForPdf(bytes, extension, cancellationToken);
        return pdf.ToMemoryFile(ContentTypes.PDF);
    }

    /// <summary>
    /// Converts the document to PDF, then rasterises it with the <see cref="IPdfToImageService"/> — one
    /// image per page.
    /// </summary>
    public async Task<IEnumerable<IImageFile>> ToImages(WordTemplateInput input, CancellationToken cancellationToken = default)
    {
        if (pdfToImages == null)
        {
            throw new InvalidOperationException(
                "ToImages rasterises the PDF Gotenberg returns, which needs an IPdfToImageService " +
                "(Regira.Office.PDF.DocNET, for example). Register one, or pass it to the constructor.");
        }

        using var pdf = await Convert(input, RegiraFileFormat.Pdf, cancellationToken);
        return await pdfToImages.ToImages(pdf, config?.ImageOptions, cancellationToken);
    }


    private static void EnsurePdf(RegiraFileFormat format)
    {
        switch (format)
        {
            case RegiraFileFormat.Pdf:
                return;
            case RegiraFileFormat.Png:
            case RegiraFileFormat.Jpeg:
                throw new NotSupportedException("Image output is not produced by Convert. Use ToImages instead.");
            default:
                throw new NotSupportedException(
                    $"Gotenberg converts to PDF only. {format} output needs a backend with a document model: " +
                    "Regira.Office.Word.Spire, Regira.Office.Word.Syncfusion or Regira.Office.Word.Aspose.");
        }
    }

    /// <summary>
    /// The bytes to upload and the extension to upload them under. An input that needs template
    /// processing is rendered by the <see cref="IWordCreator"/> first, which produces <c>.docx</c>.
    /// </summary>
    private async Task<(byte[] Bytes, string Extension)> GetSource(WordTemplateInput input, CancellationToken cancellationToken)
    {
        var templateFeatures = FindTemplateFeatures(input);
        if (templateFeatures.Count > 0)
        {
            if (creator == null)
            {
                throw new NotSupportedException(
                    $"Gotenberg converts finished documents and cannot apply {string.Join(", ", templateFeatures)}. " +
                    "Supply an IWordCreator (Regira.Office.Word.Mini, for example) to render the template first.");
            }

            using var created = await creator.Create(input, cancellationToken);
            var createdBytes = created.GetBytes()
                ?? throw new InvalidOperationException("The IWordCreator returned a document without content.");
            return (createdBytes, "docx");
        }

        var template = input.Template ?? throw new ArgumentException("Template is required.", nameof(input));
        var bytes = template.GetBytes() ?? throw new ArgumentException("Template has no content.", nameof(input));
        return (bytes, SourceFormat.Resolve(template, bytes));
    }

    /// <summary>
    /// The parts of <see cref="WordTemplateInput"/> that only a document model can honour. Collections
    /// count when they hold something: a default-initialised input has empty ones.
    /// </summary>
    private static List<string> FindTemplateFeatures(WordTemplateInput input)
    {
        var features = new List<string>();

        if (input.GlobalParameters?.Count > 0) features.Add(nameof(input.GlobalParameters));
        if (input.CollectionParameters?.Count > 0) features.Add(nameof(input.CollectionParameters));
        if (input.Images?.Count > 0) features.Add(nameof(input.Images));
        if (input.DocumentParameters?.Count > 0) features.Add(nameof(input.DocumentParameters));
        if (input.Headers?.Count > 0) features.Add(nameof(input.Headers));
        if (input.Footers?.Count > 0) features.Add(nameof(input.Footers));
        if (input.Options is { } options &&
            (options.InheritFont || options.HorizontalAlignment.HasValue || options.EnforceEvenAmountOfPages || options.RemoveEmptyParagraphs))
        {
            features.Add(nameof(input.Options));
        }

        return features;
    }

    private async Task<byte[]> PostForPdf(byte[] bytes, string extension, CancellationToken cancellationToken)
    {
        using var content = new MultipartFormDataContent();
        // Gotenberg picks the converter from the file name's extension
        content.Add(new ByteArrayContent(bytes), "files", $"document.{extension}");

        using var response = await client.PostAsync(ConvertRoute, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var hint = response.StatusCode == HttpStatusCode.ServiceUnavailable
                ? " Gotenberg answers 503 when a conversion exceeds its --api-timeout (30 seconds by default)."
                : string.Empty;
            throw new HttpRequestException(
                $"{response.RequestMessage?.RequestUri} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}{hint}",
                null,
                response.StatusCode);
        }

        return await response.Content.ReadAsByteArrayAsync(cancellationToken);
    }
}
