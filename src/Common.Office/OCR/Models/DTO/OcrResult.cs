namespace Regira.Office.OCR.Models.DTO;

public record OcrResult
{
    public string Language { get; set; } = null!;
    public string? Text { get; set; }
}