using Regira.IO.Extensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Models;
using Regira.Office.Barcodes.Models.DTO;
using Regira.Office.Clients.Abstractions;

namespace Regira.Office.Clients.Services;

public class BarcodeClient(HttpClient client) : OfficeClientBase(client), IBarcodeService
{
    private const string CreatePath = "barcode/create";
    private const string ReadPath = "barcode/read";

    public async Task<IImageFile> Create(BarcodeInput input, CancellationToken cancellationToken = default)
    {
        var dto = input.ToBarcodeInputDto();
        return await PostJsonForFileAsync(CreatePath, dto, cancellationToken);
    }

    public async Task<BarcodeReadResult?> Read(IImageFile img, BarcodeFormat? format = null, CancellationToken cancellationToken = default)
    {
        var url = format.HasValue ? $"{ReadPath}?format={format.Value}" : ReadPath;
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(img.GetBytes() ?? throw new ArgumentException("Image has no content.", nameof(img))), "barcode", "barcode.jpg");

        return await PostMultipartAsync<BarcodeReadResult>(url, content, cancellationToken);
    }
}
