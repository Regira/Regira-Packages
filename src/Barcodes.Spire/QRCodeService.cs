using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Spire;

public class QRCodeService : IQRCodeService
{
    private readonly BarcodeService _service = new();

    public Task<IImageFile> Create(QRCodeInput input, CancellationToken cancellationToken = default) => _service.Create(input, cancellationToken);
    public Task<BarcodeReadResult?> Read(IImageFile qrCode, CancellationToken cancellationToken = default) => _service.Read(qrCode, cancellationToken: cancellationToken);
}