namespace Regira.Office.Barcodes.Models.DTO;

public record QRCodeInputDto
{
    public string Content { get; set; } = null!;
    public int Size { get; set; } = 200;
    public string Color { get; set; } = "#000000";
}