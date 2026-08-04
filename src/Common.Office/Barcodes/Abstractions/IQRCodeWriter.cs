using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Abstractions;

public interface IQRCodeWriter
{
    Task<IImageFile> Create(QRCodeInput input, CancellationToken cancellationToken = default);
}