using System.Drawing;
using System.Drawing.Printing;

namespace Regira.Drawing.GDI.Utilities;

public static class PrintUtility
{
    public static void Print(Image image, PageSettings pageSettings, PrinterSettings printerSettings)
    {
        using var printDocument = new PrintDocument();
        printDocument.PrinterSettings = printerSettings;
        printDocument.DefaultPageSettings = pageSettings;

        printDocument.PrintPage += (_, e) =>
        {
            e.Graphics!.DrawImage(image, pageSettings.Bounds with { X = 0, Y = 0 });
        };

        printDocument.Print();
    }
}