namespace Regira.Web.IO;

public record BinaryFileDto
{
    public string? FileName { get; set; }
    public string? ContentType { get; set; }
    public byte[]? Bytes { get; set; }
}