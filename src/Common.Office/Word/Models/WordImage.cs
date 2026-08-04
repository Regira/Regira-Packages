using Regira.IO.Abstractions;
using Regira.Media.Drawing.Dimensions;

namespace Regira.Office.Word.Models;

public record WordImage
{
    public string Name { get; set; } = null!;
    public ImageSize? Size { get; set; }
    public IMemoryFile? File { get; set; }
    public HorizontalAlignment? HorizontalAlignment { get; set; }
}