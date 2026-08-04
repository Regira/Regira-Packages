using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;
using Regira.Media.Drawing.Models.Abstractions;

namespace Regira.Media.Drawing.Models;

public class ImageFile : IImageFile
{
    public byte[]? Bytes { get; set; }
    public Stream? Stream { get; set; }
    public long Length { get; set; }
    public ImageSize? Size { get; set; }
    public ImageFormat? Format { get; set; }
    public string? ContentType { get; set; }


    public void Dispose()
        => Stream?.Dispose();
}