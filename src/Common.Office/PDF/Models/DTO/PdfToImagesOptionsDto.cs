using Regira.Media.Drawing.Enums;

namespace Regira.Office.PDF.Models.DTO;

public record PdfToImagesOptionsDto
{
    public int? Width { get; set; }
    public int? Height { get; set; }
    public ImageFormat? Format { get; set; }
}