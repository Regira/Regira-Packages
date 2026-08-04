using Regira.IO.Abstractions;
using Regira.Media.Drawing.Dimensions;
using Regira.Media.Drawing.Enums;

namespace Regira.Media.Drawing.Models.Abstractions;

public interface IImageFile : IMemoryFile
{
    public ImageSize? Size { get; set; }
    ImageFormat? Format { get; set; }
}