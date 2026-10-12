using System.Drawing.Printing;
using Regira.Printing.GDI;
using RegiraPaperSourceKind = Regira.Office.Printing.Models.PaperSourceKind;

namespace Drawing.Testing.GDI;

#pragma warning disable CA1416
[TestFixture]
public class GdiPrintServiceTests
{
    private static readonly PaperSource Default = Tray("Default", PaperSourceKind.FormSource);
    private static readonly PaperSource[] Trays = [Tray("Auto", PaperSourceKind.AutomaticFeed), Tray("Lower", PaperSourceKind.Lower)];

    private static PaperSource Tray(string name, PaperSourceKind kind) => new() { SourceName = name, RawKind = (int)kind };

    [Test]
    public void Without_A_Tray_Uses_The_Default()
    {
        var source = PrintService.SelectPaperSource(Trays, null, Default);

        Assert.That(source, Is.SameAs(Default));
    }

    [Test]
    public void Uses_The_Requested_Tray()
    {
        var source = PrintService.SelectPaperSource(Trays, RegiraPaperSourceKind.Lower, Default);

        Assert.That(source.SourceName, Is.EqualTo("Lower"));
    }

    [Test]
    public void A_Tray_The_Printer_Lacks_Uses_The_Default()
    {
        var source = PrintService.SelectPaperSource(Trays, RegiraPaperSourceKind.Cassette, Default);

        Assert.That(source, Is.SameAs(Default));
    }
}
