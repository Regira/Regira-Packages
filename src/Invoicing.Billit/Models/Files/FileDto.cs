namespace Regira.Invoicing.Billit.Models.Files;

public class FileDto
{
    public string FileID { get; set; } = null!;
    public string FileName { get; set; } = null!;
    public string MimeType { get; set; } = null!;
    public byte[] FileContent { get; set; } = null!;
    public bool? HasDocuments { get; set; }
}