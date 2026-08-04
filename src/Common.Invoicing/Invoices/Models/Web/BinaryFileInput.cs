namespace Regira.Invoicing.Invoices.Models.Web;

public class BinaryFileInput
{
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? Bytes { get; set; }
}