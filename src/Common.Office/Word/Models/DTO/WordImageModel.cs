namespace Regira.Office.Word.Models.DTO;

public record WordImageModel
{
    public string? Name { get; set; }
    public byte[] Bytes { get; set; } = null!;
    /// <summary>
    /// Width of <see cref="WordImage.Size"/>; a size is read only when <see cref="Height"/> is given too
    /// </summary>
    public int? Width { get; set; }
    /// <summary>
    /// Height of <see cref="WordImage.Size"/>; a size is read only when <see cref="Width"/> is given too
    /// </summary>
    public int? Height { get; set; }
    public HorizontalAlignment? HorizontalAlignment { get; set; }
}