using Regira.IO.Extensions;
using Regira.Office.Printing.Abstractions;
using Regira.Office.Printing.Models;
using System.Drawing;
using System.Drawing.Printing;

namespace Regira.Printing.GDI;

public class PrintService : IPrintService
{
    public IList<string> List()
    {
        return PrinterSettings.InstalledPrinters
            .ToArray();
    }

    public void Print(ImagePrintInputModel input)
    {
        // Load image
        using var imgStream = input.Image.GetStream()!;
        var image = Image.FromStream(imgStream);

        // Configure printer settings
        var printerSettings = new PrinterSettings
        {
            PrinterName = input.PrinterName,
            PrintToFile = false,
            Collate = input.Collate ?? true,
            Copies = (short)Math.Max(1, input.Copies ?? 1),
            Duplex = (input.Duplex ?? Office.Printing.Models.Duplex.Default).Convert()
        };

        if (input is { FromPage: > 0, ToPage: > 0 } && input.ToPage >= input.FromPage)
        {
            printerSettings.FromPage = input.FromPage ?? 0;
            printerSettings.ToPage = input.ToPage ?? 0;
            printerSettings.PrintRange = PrintRange.SomePages;
        }
        else
        {
            printerSettings.PrintRange = PrintRange.AllPages;
        }

        // Match paper size by name (fallback to default if not found)
        var paperSize = printerSettings.PaperSizes.Cast<PaperSize>()
            .FirstOrDefault(ps => string.Equals(ps.PaperName, input.PaperSizeName ?? "A4", StringComparison.OrdinalIgnoreCase))
                        ?? printerSettings.DefaultPageSettings.PaperSize;

        // Match paper source
        var paperSource = printerSettings.PaperSources.Cast<PaperSource>()
            .FirstOrDefault(ps => ps.Kind == input.PaperSourceKind.Convert())
                          ?? printerSettings.DefaultPageSettings.PaperSource;

        // Configure page settings
        var pageSettings = new PageSettings(printerSettings)
        {
            Landscape = input.Landscape ?? false,
            Color = input.Color ?? false,
            Margins = (input.Margins ?? new float[] { 50, 50, 50, 50 }).Convert(),
            PaperSize = paperSize,
            PaperSource = paperSource
        };

        // Configure PrintDocument
        using var printDoc = new PrintDocument();
        printDoc.PrinterSettings = printerSettings;
        printDoc.DefaultPageSettings = pageSettings;

        printDoc.PrintPage += (_, e) =>
        {
            var m = e.MarginBounds;

            // Scale the image proportionally to fit within the margins
            if ((double)image.Width / image.Height > (double)m.Width / m.Height)
            {
                m.Height = (int)((double)image.Height / image.Width * m.Width);
            }
            else
            {
                m.Width = (int)((double)image.Width / image.Height * m.Height);
            }

            e.Graphics?.DrawImage(image, m);
        };

        // Fire off the print job
        printDoc.Print();
    }

}