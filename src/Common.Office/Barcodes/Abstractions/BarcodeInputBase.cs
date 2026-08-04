using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Office.Barcodes.Defaults;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Abstractions;

public abstract class BarcodeInputBase
{
    public string Content { get; set; } = null!;
    public BarcodeFormat Format { get; set; } = BarcodeDefaults.Format;
    public ImageSize Size { get; set; } = BarcodeDefaults.Size;
    public Color Color { get; set; } = BarcodeDefaults.Color;
    public Color BackgroundColor { get; set; } = BarcodeDefaults.BackgroundColor;
}