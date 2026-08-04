using Docnet.Core;
using Docnet.Core.Editors;
using Docnet.Core.Models;
using Regira.Collections;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Office.MimeTypes;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Defaults;
using Regira.Office.PDF.Models;
using ImageFormat = Regira.Media.Drawing.Enums.ImageFormat;

namespace Regira.Office.PDF.DocNET;

public class PdfManager(IImageService imageService) : IPdfService
{
    public Task<int> GetPageCount(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        using var docReader = DocLib.Instance.GetDocReader(pdf.GetBytes(), new PageDimensions());
        return Task.FromResult(docReader.GetPageCount());
    }


    public async Task<IEnumerable<IMemoryFile>> Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default)
    {
        var result = new List<IMemoryFile>();
        foreach (var range in ranges)
        {
            var pageCount = await GetPageCount(pdf, cancellationToken);
            var splitBytes = DocLib.Instance.Split(pdf.GetBytes(), range.Start - 1, (range.End ?? pageCount) - 1);
            var file = splitBytes.ToMemoryFile(ContentTypes.PDF);
            result.Add(file);
        }
        return result;
    }
    public IMemoryFile Merge(IEnumerable<string> pdfPaths)
    {
        var list = pdfPaths.ToArray();
        var maxBatchSize = 10;
        var batchSize = (int)Math.Ceiling(list.Length / (double)maxBatchSize);
        var partlyMergedPdfs = new List<byte[]>(batchSize);
        for (var i = 0; i < batchSize; i++)
        {
            var pdfBatch = list
                .Skip(i * maxBatchSize)
                .Take(maxBatchSize)
                .Select(File.ReadAllBytes)
                .ToArray();
            var merged = DocLib.Instance.Merge(pdfBatch);
            partlyMergedPdfs.Add(merged);
        }

        var mergedBytes = DocLib.Instance.Merge(partlyMergedPdfs.ToArray());
        return mergedBytes.ToMemoryFile(ContentTypes.PDF);
    }
    public Task<IMemoryFile?> Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default)
    {
        byte[]? resultBytes = null;
        foreach (var pdf in items)
        {
            var bytes = pdf.GetBytes();
            resultBytes = resultBytes == null ? bytes : DocLib.Instance.Merge(resultBytes, bytes);
        }

        var ms = resultBytes != null
            ? new MemoryStream(resultBytes)
            : null;
        return Task.FromResult(ms?.ToMemoryFile(ContentTypes.PDF));
    }
    public async Task<IMemoryFile?> RemovePages(IMemoryFile pdf, IEnumerable<int> pages, CancellationToken cancellationToken = default)
    {
        var pagesToRemove = pages.ToArray();

        var pageCount = await GetPageCount(pdf, cancellationToken);

        var ranges = new List<PdfSplitRange>();
        var firstPage = pagesToRemove.First();
        if (firstPage > 1)
        {
            ranges.Add(new PdfSplitRange { Start = 1 });
        }
        foreach (var page in pagesToRemove)
        {
            var prev = ranges.LastOrDefault();
            if (page == prev?.Start)
            {
                prev.Start = page + 1;
            }
            else
            {
                if (prev?.End.HasValue == false)
                {
                    prev.End = page - 1;
                }
                if (page < pageCount - 1)
                {
                    ranges.Add(new PdfSplitRange
                    {
                        Start = page + 1
                    });
                }
            }
        }

        var splitPdfs = (await Split(pdf, ranges, cancellationToken))
            .Select(f => f.ToBinaryFile())
            .ToArray();
        var merged = await Merge(splitPdfs, cancellationToken);
        splitPdfs.Dispose();
        return merged;
    }


    public async Task<string> GetText(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        var texts = (await GetTextPerPage(pdf, cancellationToken))
            .Select(line => line.Trim());
        return string.Join(Environment.NewLine, texts);
    }
    public Task<IList<string>> GetTextPerPage(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        var pageDim = new PageDimensions();
        using var docReader = DocLib.Instance.GetDocReader(pdf.GetBytes(), pageDim);

        var pageCount = docReader.GetPageCount();
        var pageTexts = new List<string>(pageCount);
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            using var pageReader = docReader.GetPageReader(pageIndex);
            var pageText = pageReader.GetText();
            pageTexts.Add(pageText);
        }

        return Task.FromResult<IList<string>>(pageTexts);
    }
    public async Task<IMemoryFile?> RemoveEmptyPages(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        var texts = await GetTextPerPage(pdf, cancellationToken);
        var emptyPages = texts
            .Select((x, i) => new { page = i + 1, isEmpty = string.IsNullOrWhiteSpace(x) })
            .Where(x => x.isEmpty)
            .Select(x => x.page)
            .ToArray();

        return emptyPages.Any()
            ? await RemovePages(pdf, emptyPages, cancellationToken)
            : pdf;
    }


    public async Task<IMemoryFile?> ImagesToPdf(ImagesInput input, CancellationToken cancellationToken = default)
    {
        if (imageService == null)
        {
            throw new NullReferenceException($"{nameof(IImageService)} is not initialized");
        }

        var maxDim = new[]
        {
            (int)(input.MaxDimensions.Width - (input.Margins.Left + input.Margins.Right)),
            (int)(input.MaxDimensions.Height - (input.Margins.Top + input.Margins.Bottom))
        };
        var jpegImages = new List<JpegImage>();
        foreach (var imgBytes in input.Images)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var imageFile = (await imageService.Parse(imgBytes, cancellationToken))!;
            var resized = await imageService.Resize(imageFile, maxDim, cancellationToken: cancellationToken);
            var jpeg = await imageService.ChangeFormat(resized, ImageFormat.Jpeg, cancellationToken);
            jpegImages.Add(new JpegImage
            {
                Bytes = jpeg.Bytes,
                Width = jpeg.Size?.Width ?? 0,
                Height = jpeg.Size?.Height ?? 0
            });
        }

        var pdfBytes = DocLib.Instance.JpegToPdf(jpegImages.ToArray());
        return pdfBytes.ToMemoryFile(ContentTypes.PDF);
    }
    public async Task<IList<IImageFile>> ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default)
    {
        var pageDimensions = new PageDimensions(
            options?.Size?.Width ?? PdfDefaults.ImageSize.Width,
            options?.Size?.Height ?? PdfDefaults.ImageSize.Height
        );

        using var docReader = DocLib.Instance.GetDocReader(pdf.GetBytes(), pageDimensions);
        var pageCount = docReader.GetPageCount();
        var images = new List<IImageFile>(pageCount);
        for (var pageIndex = 0; pageIndex < pageCount; pageIndex++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            using var pr = docReader.GetPageReader(pageIndex);
            var imgBytes = pr.GetImage();

            var width = pr.GetPageWidth();
            var height = pr.GetPageHeight();

            images.Add((await imageService.Parse(imgBytes, new ImageSize(width, height), options?.Format, cancellationToken))!);
        }
        return images;
    }
}
