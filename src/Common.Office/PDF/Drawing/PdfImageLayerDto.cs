using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models.DTO;

namespace Regira.Office.PDF.Drawing;

public class PdfImageLayerDto
{
    public byte[] Bytes { get; set; } = null!;
    public int? Page { get; set; }
    public ImageFormat? Format { get; set; }
    public ImageLayerOptionsDto? DrawOptions { get; set; }
}