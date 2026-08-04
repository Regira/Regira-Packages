using Jds2;
using Regira.IO.Extensions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Printer;
using System.Drawing.Printing;

namespace Regira.Office.PDF.PockyBum522;

public class PdfPrinter : IPdfPrinter
{
    public string DefaultPrinter => new PrinterSettings().PrinterName;

    public Task<IList<string>> List(CancellationToken token = default)
    {
        var items = PrinterSettings.InstalledPrinters.Cast<string>();
        return Task.FromResult<IList<string>>(items.ToList());
    }

    public Task Print(PdfPrinterInput input, CancellationToken token = default)
    {
        var tempPath = Path.GetTempFileName();
        using (var fs = File.Create(tempPath))
        {
            using var pdfStream = input.PdfFile.GetStream()!;
            pdfStream.CopyTo(fs);
        }

        try
        {
            var pdfPrinter = new SimpleFreePdfPrinter();

            if (string.IsNullOrWhiteSpace(input.PrinterName))
            {
                pdfPrinter.PrintPdfToDefaultPrinter(tempPath);
            }
            else
            {
                pdfPrinter.PrintPdfTo(input.PrinterName, tempPath);
            }
        }
        finally
        {
            File.Delete(tempPath);
        }

        return Task.CompletedTask;
    }
}