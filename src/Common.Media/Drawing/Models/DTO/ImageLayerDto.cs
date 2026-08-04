namespace Regira.Media.Drawing.Models.DTO;

public record ImageLayerDto
{
    public byte[] Bytes { get; set; } = null!;
    public ImageLayerOptionsDto? DrawOptions { get; set; }
}