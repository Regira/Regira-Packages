using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Clients.Abstractions;
using Regira.Office.OCR.Abstractions;
using Regira.Office.OCR.Models.DTO;

namespace Regira.Office.Clients.Services;

public class OcrClient(HttpClient client) : OfficeClientBase(client), IOcrService
{
    private const string Path = "image2text";

    public async Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken cancellationToken = default)
    {
        var url = lang != null ? $"{Path}?lang={lang}" : Path;
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(imgFile.GetBytes() ?? throw new ArgumentException("Image has no content.", nameof(imgFile))), "imgFile", "image.jpg");
        var result = await PostMultipartAsync<OcrResult>(url, content, cancellationToken);
        return result!;
    }
}
