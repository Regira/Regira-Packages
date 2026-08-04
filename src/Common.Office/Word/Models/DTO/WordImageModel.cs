namespace Regira.Office.Word.Models.DTO;

public record WordImageModel
{
    public string? Name { get; set; }
    public byte[] Bytes { get; set; } = null!;
}