using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Models;
using BarcodeFormat = Regira.Office.Barcodes.Models.BarcodeFormat;

namespace Regira.Office.Barcodes.ZXing;

public class QRCodeService : IQRCodeService
{
    private readonly BarcodeService _barcodeService = new();


    public Task<IImageFile> Create(QRCodeInput input, CancellationToken cancellationToken = default) => _barcodeService.Create(input, cancellationToken);
    public Task<BarcodeReadResult?> Read(IImageFile qrCode, CancellationToken cancellationToken = default) => _barcodeService.Read(qrCode, BarcodeFormat.QRCode, cancellationToken);
}