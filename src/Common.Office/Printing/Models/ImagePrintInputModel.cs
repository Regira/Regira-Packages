using Regira.Media.Drawing.Models;
using Regira.Office.Models;

namespace Regira.Office.Printing.Models;

public class ImagePrintInputModel
{
    // Required
    public ImageFile Image { get; set; } = null!;
    public string PrinterName { get; set; } = null!;

    // PrinterSettings
    public int? Copies { get; set; } = 1;
    public bool? Collate { get; set; } = true;
    public int? FromPage { get; set; }
    public int? ToPage { get; set; }
    public Duplex? Duplex { get; set; }

    // PageSettings
    public bool? Landscape { get; set; }
    public string? PaperSizeName { get; set; }
    public PaperSourceKind? PaperSourceKind { get; set; }
    public bool? Color { get; set; }
    public Margins? Margins { get; set; }
}

public enum Duplex
{
    /// <summary>
    ///  The printer's default duplex setting.
    /// </summary>
    Default = 0,
    /// <summary>
    ///  Single-sided printing.
    /// </summary>
    Simplex,
    /// <summary>
    ///  Double-sided, horizontal printing.
    /// </summary>
    Horizontal,
    /// <summary>
    ///  Double-sided, vertical printing.
    /// </summary>
    Vertical
}

public enum PaperSourceKind
{
    /// <summary>
    ///  The upper bin of a printer (or, if the printer only has one bin, the only bin).
    /// </summary>
    Upper,

    /// <summary>
    ///  The lower bin of a printer.
    /// </summary>
    Lower,

    /// <summary>
    ///  The middle bin of a printer.
    /// </summary>
    Middle,

    /// <summary>
    ///  Manually-fed paper.
    /// </summary>
    Manual,

    /// <summary>
    ///  An envelope.
    /// </summary>
    Envelope,

    /// <summary>
    ///  A manually-fed envelope.
    /// </summary>
    ManualFeed,

    /// <summary>
    ///  Automatic-fed paper.
    /// </summary>
    AutomaticFeed,

    /// <summary>
    ///  A tractor feed.
    /// </summary>
    TractorFeed,

    /// <summary>
    ///  Small-format paper.
    /// </summary>
    SmallFormat,

    /// <summary>
    ///  Large-format paper.
    /// </summary>
    LargeFormat,

    /// <summary>
    ///  A large-capacity bin printer.
    /// </summary>
    LargeCapacity,

    /// <summary>
    ///  A paper cassette.
    /// </summary>
    Cassette,

    FormSource,

    /// <summary>
    ///  A printer-specific paper source.
    /// </summary>
    Custom
}