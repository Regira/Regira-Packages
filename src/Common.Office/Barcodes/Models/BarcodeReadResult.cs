namespace Regira.Office.Barcodes.Models;

public record BarcodeReadResult
{
    public BarcodeFormat? Format { get; set; }
    public string[]? Contents { get; set; }
}