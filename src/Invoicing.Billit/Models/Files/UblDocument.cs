namespace Regira.Invoicing.Billit.Models.Files;

public class UblDocument
{
    public string FileName { get; set; } = null!;
    public byte[] Content { get; set; } = null!;
    public string? ContentType { get; set; }
}