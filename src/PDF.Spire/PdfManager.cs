using Regira.Drawing.GDI.Utilities;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.MimeTypes;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Spire.Pdf;
using Spire.Pdf.Graphics;
using Spire.Pdf.Texts;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;

namespace Regira.Office.PDF.Spire;

public class PdfManager : IPdfMerger, IPdfSplitter, IPdfToImageService, IPdfTextExtractor
{
    public Task<int> GetPageCount(IMemoryFile pdfStream, CancellationToken cancellationToken = default)
    {
        using var doc = new PdfDocument(pdfStream.GetStream());
        return Task.FromResult(doc.Pages.Count);
    }

    public Task<IEnumerable<IMemoryFile>> Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default)
    {
        var result = new List<IMemoryFile>();
        using var doc = new PdfDocument(pdf.GetStream());
        foreach (var range in ranges)
        {
            using var split = new PdfDocument();
            for (var i = range.Start - 1; i < (range.End ?? doc.Pages.Count); i++)
            {
                var page = split.Pages.Add(doc.Pages[i].Size, new PdfMargins(0));
                doc.Pages[i].CreateTemplate().Draw(page, new PointF(0, 0));
            }

            result.Add(ToMemoryFile(split));
        }
        return Task.FromResult<IEnumerable<IMemoryFile>>(result);
    }

    public IMemoryFile Merge(IEnumerable<string> pdfPaths)
    {
        using var merged = PdfDocument.MergeFiles(pdfPaths.ToArray());
        var ms = new MemoryStream();
        merged.Save(ms);
        // rewind, otherwise consumers read from the end of the stream
        ms.Position = 0;
        return ms.ToMemoryFile(ContentTypes.PDF);
    }
    public Task<IMemoryFile?> Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default)
    {
        using var merged = new PdfDocument();
        // inserted pages draw from their source document, so every source stays open until the save in ToMemoryFile
        var docs = new List<PdfDocument>();
        try
        {
            foreach (var pdfStream in items)
            {
                var doc = new PdfDocument(pdfStream.GetStream());
                docs.Add(doc);
                for (var i = 0; i < doc.Pages.Count; i++)
                {
                    merged.InsertPage(doc, i);
                }
            }

            return Task.FromResult<IMemoryFile?>(ToMemoryFile(merged));
        }
        finally
        {
            foreach (var doc in docs)
            {
                doc.Dispose();
            }
        }
    }

    public Task<string> GetText(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        using var doc = new PdfDocument(pdf.GetStream());
        var text = new StringBuilder(doc.Pages.Count);
        foreach (PdfPageBase page in doc.Pages)
        {
            var extractor = new PdfTextExtractor(page);
            var content = extractor.ExtractText(new PdfTextExtractOptions());
            text.Append(content);
        }

        return Task.FromResult(text.ToString());
    }
    public Task<IList<IImageFile>> ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default)
    {
        var images = new List<IImageFile>();
        using var doc = new PdfDocument(pdf.GetStream());
        var pageCount = doc.Pages.Count;
        for (var i = 0; i < pageCount; i++)
        {
            using var image = doc.SaveAsImage(i);
            if (options?.Size.HasValue == true)
            {
                using var resized = GdiUtility.Resize(image, options.Size.Value.ToGdiSize());
                images.Add(resized.ToImageFile(ImageFormat.Jpeg));
            }
            else
            {
                images.Add(image.ToImageFile(ImageFormat.Jpeg));
            }
        }
        return Task.FromResult<IList<IImageFile>>(images);
    }

    public IMemoryFile ToMemoryFile(PdfDocument doc)
    {
        var ms = new MemoryStream();
        doc.SaveToStream(ms);
        // rewind, otherwise consumers read from the end of the stream
        ms.Position = 0;
        return ms.ToMemoryFile(ContentTypes.PDF);
    }
}