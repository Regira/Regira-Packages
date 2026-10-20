namespace Regira.Invoicing.Billit.Models.Files;

[Obsolete("Nothing in Regira.Invoicing.Billit reads or writes it. Will be removed in v7.")]
public class UblDocument
{
    public string FileName { get; set; } = null!;
    public byte[] Content { get; set; } = null!;
    public string? ContentType { get; set; }
}