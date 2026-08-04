using QRCodeDecoderLibrary;
using QRCodeEncoderLibrary;
using Regira.Drawing.GDI.Utilities;
using Regira.Media.Drawing.Models.Abstractions;
using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Exceptions;
using Regira.Office.Barcodes.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Text;
using ErrorCorrection = QRCodeEncoderLibrary.ErrorCorrection;

namespace Regira.Office.Barcodes.UziGranot;

public class QRCodeService : IQRCodeService
{
    public Task<IImageFile> Create(QRCodeInput input, CancellationToken cancellationToken = default)
    {
        var moduleSize = 4;

        var encoder = new QRCodeEncoder
        {
            ErrorCorrection = ErrorCorrection.M,
            ModuleSize = moduleSize,
            QuietZone = moduleSize * 4,
            EciAssignValue = -1
        };

        try
        {
            encoder.Encode(input.Content);
        }
        catch (ApplicationException ex)
        {
            if (ex.Message == "Input data string is too long")
            {
                throw new InputException(input.Content, ex);
            }
        }

        using var ms = new MemoryStream();
        encoder.SaveQRCodeToPngFile(ms);

        // resize image
        using var img = GdiUtility.ResizeFixed(Image.FromStream(ms), input.Size.ToGdiSize());
        return Task.FromResult<IImageFile>(img.ToImageFile(ImageFormat.Jpeg));
    }

    public Task<BarcodeReadResult?> Read(IImageFile qrCode, CancellationToken cancellationToken = default)
    {
        var decoder = new QRDecoder();
        using var img = qrCode.ToBitmap();
        using var bitmap = new Bitmap(img);
        var data = decoder.ImageDecoder(bitmap);
        var contents = QRCodeResult(data);

        return Task.FromResult<BarcodeReadResult?>(new BarcodeReadResult
        {
            Contents = contents,
            Format = BarcodeFormat.QRCode
        });
    }


    private static string[] QRCodeResult(byte[][]? dataByteArray)
    {
        // no QR code
        if (dataByteArray == null)
        {
            return [];
        }

        // image has one QR code
        if (dataByteArray.Length == 1)
        {
            return [ForDisplay(QRDecoder.ByteArrayToStr(dataByteArray.First()))];
        }

        // image has more than one QR code
        return dataByteArray.Select(data => ForDisplay(QRDecoder.ByteArrayToStr(data))).ToArray();
    }
    private static string ForDisplay(string result)
    {
        int index;
        for (index = 0; index < result.Length && (result[index] >= ' ' && result[index] <= '~' || result[index] >= 160); index++)
        {
            // continue;
        }
        if (index == result.Length)
        {
            return result;
        }

        var display = new StringBuilder(result.Substring(0, index));
        for (; index < result.Length; index++)
        {
            var oneChar = result[index];
            if (oneChar >= ' ' && oneChar <= '~' || oneChar >= 160)
            {
                display.Append(oneChar);
                continue;
            }

            if (oneChar == '\r')
            {
                display.Append("\r\n");
                if (index + 1 < result.Length && result[index + 1] == '\n')
                {
                    index++;
                }
                continue;
            }

            if (oneChar == '\n')
            {
                display.Append("\r\n");
                continue;
            }

            display.Append('¿');
        }
        return display.ToString();
    }
}
