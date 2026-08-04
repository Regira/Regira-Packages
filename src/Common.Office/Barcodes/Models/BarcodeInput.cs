using Regira.Office.Barcodes.Abstractions;
using Regira.Office.Barcodes.Defaults;

namespace Regira.Office.Barcodes.Models;

public class BarcodeInput : BarcodeInputBase
{
    public BarcodeInput()
    {
        Format = BarcodeDefaults.Format;
        Color = BarcodeDefaults.Color;
        BackgroundColor = BarcodeDefaults.BackgroundColor;
    }

    public static implicit operator BarcodeInput(string content) => new() { Content = content };
    public static implicit operator string?(BarcodeInput? input) => input?.Content;
}