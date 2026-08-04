using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Models;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Defaults;

public static class BarcodeDefaults
{
    public static BarcodeFormat Format { get; set; } = BarcodeFormat.Code128;
    public static ImageSize Size { get; set; } = new(400, 100);
    public static Color Color { get; set; } = Color.Black;
    public static Color BackgroundColor { get; set; } = Color.White;
}