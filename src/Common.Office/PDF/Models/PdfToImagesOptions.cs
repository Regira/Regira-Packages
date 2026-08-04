using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Office.PDF.Defaults;

namespace Regira.Office.PDF.Models;

public class PdfToImagesOptions
{
    public ImageSize? Size { get; set; } = PdfDefaults.ImageSize;
    public ImageFormat Format { get; set; } = ImageFormat.Jpeg;
}