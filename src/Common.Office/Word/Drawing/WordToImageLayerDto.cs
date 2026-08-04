using Regira.Media.Drawing.Models.DTO;

namespace Regira.Office.Word.Drawing;

public class WordToImageLayerDto
{
    public byte[] Bytes { get; set; } = null!;
    public int? Page { get; set; }
    public ImageLayerOptionsDto? DrawOptions { get; set; }
}