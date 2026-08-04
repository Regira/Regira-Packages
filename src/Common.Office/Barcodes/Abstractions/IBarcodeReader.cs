using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Abstractions;

public interface IBarcodeReader
{
    Task<BarcodeReadResult?> Read(IImageFile img, BarcodeFormat? format = null, CancellationToken cancellationToken = default);
}