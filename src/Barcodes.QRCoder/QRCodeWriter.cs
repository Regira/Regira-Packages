using System.Drawing;
using System.Drawing.Imaging;
using QRCoder;
using QRCoder.Exceptions;
using Regira.Drawing.GDI.Utilities;
using Regira.IO.Extensions;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Media.Drawing.Utilities;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Exceptions;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.QRCoder;

public class QRCodeWriter : IQRCodeWriter
{
    public Task<IImageFile> Create(QRCodeInput input, CancellationToken cancellationToken = default)
    {
        var generator = new QRCodeGenerator();
        QRCodeData qrData;
        try
        {
            qrData = generator.CreateQrCode(input.Content, QRCodeGenerator.ECCLevel.Q);
        }
        catch (DataTooLongException ex)
        {
            throw new InputException(ex.Message, ex);
        }

        var qrCode = new PngByteQRCode(qrData);
        var img = qrCode.GetGraphic(10, true).ToBinaryFile().ToImageFile();
        var width = input.Size.Width;
        var height = input.Size.Height;
        using var resizedImg = GdiUtility.Resize(img.ToBitmap(), new Size(width, height));
        return Task.FromResult<IImageFile>(resizedImg.ToImageFile(ImageFormat.Jpeg));
    }
}