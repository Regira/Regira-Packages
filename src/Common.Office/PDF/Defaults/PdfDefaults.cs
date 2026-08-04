using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;

namespace Regira.Office.PDF.Defaults;

public static class PdfDefaults
{
    public static ImageSize ImageSize { get; set; } = new (1080, 1920);
    public static ImageFormat ImageFormat { get; set; } = ImageFormat.Jpeg;
}