using Regira.IO.Abstractions;

namespace Regira.Office.Word.Drawing;

public class WordToImageLayerOptions
{
    public IMemoryFile File { get; set; } = null!;
    public int? Page { get; set; }
}