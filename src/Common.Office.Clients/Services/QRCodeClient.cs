using Regira.IO.Extensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Models;
using Regira.Office.Barcodes.Models.DTO;
using Regira.Office.Clients.Abstractions;

namespace Regira.Office.Clients.Services;

public class QRCodeClient(HttpClient client) : OfficeClientBase(client), IQRCodeService
{
    private const string CreatePath = "qrcode/create";
    private const string ReadPath = "qrcode/read";

    public async Task<IImageFile> Create(QRCodeInput input, CancellationToken cancellationToken = default)
    {
        var dto = input.ToQRCodeInputDto();
        return await PostJsonForFileAsync(CreatePath, dto, cancellationToken);
    }

    public async Task<BarcodeReadResult?> Read(IImageFile qrCode, CancellationToken cancellationToken = default)
    {
        using var content = new MultipartFormDataContent();
        content.Add(new ByteArrayContent(qrCode.GetBytes() ?? throw new ArgumentException("Image has no content.", nameof(qrCode))), "file", "qrcode.jpg");
        var text = await PostMultipartForTextAsync(ReadPath, content, cancellationToken);
        return text == null ? null : new BarcodeReadResult { Format = BarcodeFormat.QRCode, Contents = [text] };
    }
}
