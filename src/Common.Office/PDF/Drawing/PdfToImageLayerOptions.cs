using Regira.IO.Abstractions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Office.PDF.Defaults;

namespace Regira.Office.PDF.Drawing;

public class PdfToImageLayerOptions
{
    public IMemoryFile File { get; set; } = null!;
    public int? Page { get; set; }
    public ImageSize? Size { get; set; } = PdfDefaults.ImageSize;
    public ImageFormat Format { get; set; } = PdfDefaults.ImageFormat;
}