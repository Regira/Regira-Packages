namespace Regira.Office.Barcodes.Models.DTO;

public record BarcodeInputDto
{
    public BarcodeFormat? Format { get; set; }
    public string Content { get; set; } = null!;
    public int? Width { get; set; }
    public int? Height { get; set; }
    public string? Color { get; set; }
    public string? BackgroundColor { get; set; }
}