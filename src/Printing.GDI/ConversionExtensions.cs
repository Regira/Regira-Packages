using System.Drawing.Printing;
using RegiraDuplex = Regira.Office.Printing.Models.Duplex;
using RegiraMargins = Regira.Office.Models.Margins;
using RegiraPaperSourceKind = Regira.Office.Printing.Models.PaperSourceKind;

namespace Regira.Printing.GDI;

public static class ConversionExtensions
{
    public static Duplex Convert(this Office.Printing.Models.Duplex duplex)
        => duplex switch
        {
            RegiraDuplex.Simplex => Duplex.Simplex,
            RegiraDuplex.Horizontal => Duplex.Horizontal,
            RegiraDuplex.Vertical => Duplex.Vertical,

            _ => Duplex.Default
        };

    public static PaperSourceKind Convert(this RegiraPaperSourceKind? input)
        => input switch
        {
            RegiraPaperSourceKind.Upper => PaperSourceKind.Upper,
            RegiraPaperSourceKind.Lower => PaperSourceKind.Lower,
            RegiraPaperSourceKind.Middle => PaperSourceKind.Middle,
            RegiraPaperSourceKind.Manual => PaperSourceKind.Manual,
            RegiraPaperSourceKind.Envelope => PaperSourceKind.Envelope,
            RegiraPaperSourceKind.ManualFeed => PaperSourceKind.ManualFeed,
            RegiraPaperSourceKind.TractorFeed => PaperSourceKind.TractorFeed,
            RegiraPaperSourceKind.SmallFormat => PaperSourceKind.SmallFormat,
            RegiraPaperSourceKind.LargeFormat => PaperSourceKind.LargeFormat,
            RegiraPaperSourceKind.LargeCapacity => PaperSourceKind.LargeCapacity,
            RegiraPaperSourceKind.Cassette => PaperSourceKind.Cassette,
            RegiraPaperSourceKind.FormSource => PaperSourceKind.FormSource,
            RegiraPaperSourceKind.Custom => PaperSourceKind.Custom,

            _ => PaperSourceKind.AutomaticFeed
        };

    public static Margins Convert(this RegiraMargins margins)
        => new((int)margins.Left, (int)margins.Right, (int)margins.Top, (int)margins.Bottom);
}