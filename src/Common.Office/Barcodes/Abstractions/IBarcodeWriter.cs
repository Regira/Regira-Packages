using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Abstractions;

public interface IBarcodeWriter
{
    Task<IImageFile> Create(BarcodeInput input, CancellationToken cancellationToken = default);
}