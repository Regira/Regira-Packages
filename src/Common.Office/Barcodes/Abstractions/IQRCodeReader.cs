using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Abstractions;

public interface IQRCodeReader
{
    Task<BarcodeReadResult?> Read(IImageFile qrCode, CancellationToken cancellationToken = default);
}