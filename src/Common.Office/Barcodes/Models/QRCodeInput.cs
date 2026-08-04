using Regira.Office.Barcodes.Defaults;

namespace Regira.Office.Barcodes.Models;

public class QRCodeInput : BarcodeInput
{
    public QRCodeInput()
    {
        Format = BarcodeFormat.QRCode;
        Size = BarcodeDefaults.Size.Width;
    }

    public static implicit operator QRCodeInput(string content) => new() { Content = content };
    public static implicit operator string?(QRCodeInput? input) => input?.Content;
}