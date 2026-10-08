using PDFtoImage;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Office.MimeTypes;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Defaults;
using Regira.Office.PDF.Models;
using SkiaSharp;
using UglyToad.PdfPig;
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.DocumentLayoutAnalysis.TextExtractor;
using UglyToad.PdfPig.Writer;

namespace Regira.Office.PDF.PdfPig;

/// <summary>
/// PDF operations on <see href="https://github.com/UglyToad/PdfPig">PdfPig</see>, which is fully managed: merging,
/// splitting, removing pages, text extraction and images to PDF. Page images are rendered by
/// <see href="https://github.com/sungaila/PDFtoImage">PDFtoImage</see> over PDFium, which ships native binaries for
/// Windows, Linux and macOS.
/// </summary>
/// <param name="imageService">Parses the images that <see cref="ImagesToPdf"/> places and the page images
/// <see cref="ToImages"/> returns, and converts them between formats.</param>
public class PdfService(IImageService imageService) : IPdfService
{
    private const float PdfPointsPerInch = 72f;

    public Task<int> GetPageCount(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(GetBytes(pdf));
        return Task.FromResult(document.NumberOfPages);
    }

    public async Task<IEnumerable<IMemoryFile>> Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default)
    {
        var bytes = GetBytes(pdf);
        var pageCount = await GetPageCount(pdf, cancellationToken);
        var result = new List<IMemoryFile>();
        foreach (var range in ranges)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pages = Enumerable.Range(range.Start, (range.End ?? pageCount) - range.Start + 1).ToArray();
            result.Add(CopyPages(bytes, pages));
        }
        return result;
    }
    public Task<IMemoryFile?> Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default)
    {
        var files = items.Select(GetBytes).ToArray();
        if (files.Length == 0)
        {
            return Task.FromResult<IMemoryFile?>(null);
        }

        var merged = PdfMerger.Merge(files);
        return Task.FromResult<IMemoryFile?>(merged.ToMemoryFile(ContentTypes.PDF));
    }
    public async Task<IMemoryFile?> RemovePages(IMemoryFile pdf, IEnumerable<int> pages, CancellationToken cancellationToken = default)
    {
        var pagesToRemove = pages.ToHashSet();
        var pageCount = await GetPageCount(pdf, cancellationToken);
        var pagesToKeep = Enumerable.Range(1, pageCount)
            .Where(page => !pagesToRemove.Contains(page))
            .ToArray();

        return pagesToKeep.Length == 0
            ? null
            : CopyPages(GetBytes(pdf), pagesToKeep);
    }


    public async Task<string> GetText(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        var texts = (await GetTextPerPage(pdf, cancellationToken))
            .Select(text => text.Trim());
        return string.Join(Environment.NewLine, texts);
    }
    public Task<IList<string>> GetTextPerPage(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        using var document = PdfDocument.Open(GetBytes(pdf));
        var texts = new List<string>(document.NumberOfPages);
        foreach (var page in document.GetPages())
        {
            cancellationToken.ThrowIfCancellationRequested();
            texts.Add(ContentOrderTextExtractor.GetText(page));
        }
        return Task.FromResult<IList<string>>(texts);
    }
    public async Task<IMemoryFile?> RemoveEmptyPages(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        var texts = await GetTextPerPage(pdf, cancellationToken);
        var emptyPages = texts
            .Select((text, i) => new { page = i + 1, isEmpty = string.IsNullOrWhiteSpace(text) })
            .Where(x => x.isEmpty)
            .Select(x => x.page)
            .ToArray();

        return emptyPages.Any()
            ? await RemovePages(pdf, emptyPages, cancellationToken)
            : pdf;
    }


    /// <summary>
    /// Places each image on a page of its own, of the input's <see cref="PdfInputBase.Format"/> and
    /// <see cref="PdfInputBase.Orientation"/>. The image is centred horizontally between the
    /// <see cref="PdfInputBase.Margins"/> and starts at the top margin, at one pixel per <see cref="PdfInputBase.DPI"/>
    /// unit, scaled down when it does not fit — never up.
    /// JPEG and PNG images are embedded as they are; any other format is converted to PNG first.
    /// </summary>
    public async Task<IMemoryFile?> ImagesToPdf(ImagesInput input, CancellationToken cancellationToken = default)
    {
        if (input.Images.Count == 0)
        {
            return null;
        }

        // ImagesInput measures in units of its DPI; a PDF measures in points, 72 to the inch
        var scale = PdfPointsPerInch / input.DPI;
        var page = input.MaxDimensions;
        var margins = input.Margins;
        var availableWidth = page.Width - margins.Left - margins.Right;
        var availableHeight = page.Height - margins.Top - margins.Bottom;

        var builder = new PdfDocumentBuilder();
        foreach (var bytes in input.Images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var image = await imageService.Parse(bytes, cancellationToken)
                ?? throw new ArgumentException("An image could not be read.", nameof(input));
            var format = await imageService.GetFormat(image, cancellationToken);
            if (format != ImageFormat.Jpeg && format != ImageFormat.Png)
            {
                image = await imageService.ChangeFormat(image, ImageFormat.Png, cancellationToken);
                format = ImageFormat.Png;
            }

            var width = (float)image.Size!.Value.Width;
            var height = (float)image.Size!.Value.Height;
            var fit = Math.Min(1, Math.Min(availableWidth / width, availableHeight / height));
            width *= fit;
            height *= fit;
            var left = margins.Left + (availableWidth - width) / 2;
            var top = margins.Top;

            var pageBuilder = builder.AddPage(page.Width * scale, page.Height * scale);
            // PDF coordinates start at the bottom left
            var placement = new PdfRectangle(
                left * scale,
                (page.Height - top - height) * scale,
                (left + width) * scale,
                (page.Height - top) * scale);
            if (format == ImageFormat.Jpeg)
            {
                pageBuilder.AddJpeg(image.GetBytes()!, placement);
            }
            else
            {
                pageBuilder.AddPng(image.GetBytes()!, placement);
            }
        }

        return builder.Build().ToMemoryFile(ContentTypes.PDF);
    }
    /// <summary>
    /// Renders each page to an image that fits <see cref="PdfToImagesOptions.Size"/> either way round: the page's
    /// shorter side is fitted to the smaller of the two dimensions and its longer side to the larger, keeping the
    /// page's aspect ratio. Annotations and filled-in form fields are drawn, as a PDF viewer shows them.
    /// </summary>
    public async Task<IList<IImageFile>> ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default)
    {
        var size = options?.Size ?? PdfDefaults.ImageSize;
        var format = options?.Format ?? PdfDefaults.ImageFormat;
        var shortSide = Math.Min(size.Width, size.Height);
        var longSide = Math.Max(size.Width, size.Height);

        var bytes = GetBytes(pdf);
        var pageSizes = Conversion.GetPageSizes(bytes);
        var images = new List<IImageFile>(pageSizes.Count);
        for (var pageIndex = 0; pageIndex < pageSizes.Count; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var pageSize = pageSizes[pageIndex];
            var fit = Math.Min(
                shortSide / Math.Min(pageSize.Width, pageSize.Height),
                longSide / Math.Max(pageSize.Width, pageSize.Height));
            var renderOptions = new RenderOptions
            {
                Width = Math.Max(1, (int)Math.Round(pageSize.Width * fit)),
                Height = Math.Max(1, (int)Math.Round(pageSize.Height * fit)),
                WithAnnotations = true,
                WithFormFill = true
            };

            using var bitmap = Conversion.ToImage(bytes, pageIndex, options: renderOptions);
            using var png = bitmap.Encode(SKEncodedImageFormat.Png, 100);
            var image = await imageService.Parse(png.ToArray(), cancellationToken)
                ?? throw new InvalidOperationException($"Page {pageIndex + 1} could not be rendered.");
            // parsing reads the pixels, not the encoding
            image.Format = ImageFormat.Png;
            image.ContentType = "image/png";
            if (format != ImageFormat.Png)
            {
                image = await imageService.ChangeFormat(image, format, cancellationToken);
            }
            images.Add(image);
        }
        return images;
    }


    private static IMemoryFile CopyPages(byte[] pdf, IReadOnlyList<int> pages)
        => PdfMerger.Merge([pdf], [pages]).ToMemoryFile(ContentTypes.PDF);

    private static byte[] GetBytes(IMemoryFile pdf)
        => pdf.GetBytes() ?? throw new ArgumentException("The PDF has no content.", nameof(pdf));
}
