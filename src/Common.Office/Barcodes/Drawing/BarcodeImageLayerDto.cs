using Regira.Media.Drawing.Models.DTO;
using Regira.Office.Barcodes.Models;

namespace Regira.Office.Barcodes.Drawing;

public class BarcodeImageLayerDto
{
    public string Content { get; set; } = null!;
    public BarcodeFormat? Format { get; set; }
    public string? Color { get; set; }
    public string? BackgroundColor { get; set; }
    public ImageLayerOptionsDto? DrawOptions { get; set; }
}