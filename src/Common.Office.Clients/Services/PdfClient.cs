using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Clients.Abstractions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Regira.Office.PDF.Models.DTO;

namespace Regira.Office.Clients.Services;

public class PdfClient(HttpClient client) : OfficeClientBase(client),
    IHtmlToPdfService, IImagesToPdfService, IPdfMerger, IPdfSplitter, IPdfTextExtractor, IPdfToImageService
{
    private const string FromHtmlPath = "pdf/from-html";
    private const string FromImagesPath = "pdf/from-images";
    private const string ToImagesPath = "pdf/to-images";
    private const string MergePath = "pdf/merge";
    private const string SplitPath = "pdf/split";
    private const string TextPath = "pdf/txt";
    private const string PageCountPath = "pdf/page-count";

    public async Task<IMemoryFile> Create(HtmlInput input, CancellationToken cancellationToken = default)
    {
        var dto = input.ToHtmlToPdfInput();
        return await PostJsonForFileAsync(FromHtmlPath, dto, cancellationToken);
    }

    public async Task<IMemoryFile?> ImagesToPdf(ImagesInput input, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var i = 0;
        foreach (var imageBytes in input.Images)
            content.Add(new ByteArrayContent(imageBytes), "images", $"image{i++}.jpg");
        return await PostMultipartForFileAsync(FromImagesPath, content, cancellationToken);
    }

    public async Task<IMemoryFile?> Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        var i = 0;
        foreach (var file in items)
            content.Add(new ByteArrayContent(file.GetBytes() ?? throw new ArgumentException("File has no content.")), "files", $"file{i++}.pdf");
        return await PostMultipartForFileAsync(MergePath, content, cancellationToken);
    }

    public async Task<IEnumerable<IMemoryFile>> Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default)
    {
        var pdfBytes = pdf.GetBytes() ?? throw new ArgumentException("File has no content.", nameof(pdf));
        var results = new List<IMemoryFile>();
        foreach (var range in ranges)
        {
            var rangeParam = range.End.HasValue ? $"{range.Start}-{range.End}" : $"{range.Start}";
            using var content = new MultipartFormDataContent();
            content.Add(new ByteArrayContent(pdfBytes), "pdfFile", "file.pdf");
            results.Add(await PostMultipartForFileAsync($"{SplitPath}?ranges={rangeParam}", content, cancellationToken));
        }
        return results;
    }

    public async Task<int> GetPageCount(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(pdf.GetBytes() ?? throw new ArgumentException("File has no content.", nameof(pdf))), "files", "file.pdf");
        var counts = await PostMultipartAsync<Dictionary<string, int>>(PageCountPath, content, cancellationToken);
        return counts?.Values.Sum() ?? 0;
    }

    public async Task<string> GetText(IMemoryFile pdf, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(pdf.GetBytes() ?? throw new ArgumentException("File has no content.", nameof(pdf))), "files", "file.pdf");
        return await PostMultipartForTextAsync(TextPath, content, cancellationToken) ?? string.Empty;
    }

    public async Task<IList<IImageFile>> ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default)
    {
        var url = BuildToImagesUrl(options);
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(pdf.GetBytes() ?? throw new ArgumentException("File has no content.", nameof(pdf))), "pdfFile", "file.pdf");
        var files = await PostMultipartForFilesAsync(url, content, cancellationToken);
        return files.Cast<IImageFile>().ToList();
    }

    private static string BuildToImagesUrl(PdfToImagesOptions? input)
    {
        if (input == null) return ToImagesPath;
        var parts = new List<string>();
        if (input.Size?.Width > 0) parts.Add($"Width={input.Size.Value.Width}");
        if (input.Size?.Height > 0) parts.Add($"Height={input.Size.Value.Height}");
        parts.Add($"Format={input.Format}");
        return parts.Count == 0 ? ToImagesPath : $"{ToImagesPath}?{string.Join("&", parts)}";
    }
}
