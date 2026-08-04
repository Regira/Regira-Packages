using PDFtoPrinter;
using Regira.IO.Extensions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Printer;
using System.Drawing.Printing;

namespace Regira.Office.PDF.PDFtoPrinter;

public class PdfPrinter : IPdfPrinter
{
    public string DefaultPrinter => new PrinterSettings().PrinterName;

    public Task<IList<string>> List(CancellationToken token = default)
    {
        var items = PrinterSettings.InstalledPrinters.Cast<string>().ToList();
        return Task.FromResult<IList<string>>(items);
    }

    public Task Print(PdfPrinterInput input, CancellationToken token = default)
    {
        var printer = new PDFtoPrinterPrinter();
        var tempPath = Path.GetTempFileName();
        using (var fs = File.Create(tempPath))
        {
            using var pdfStream = input.PdfFile.GetStream()!;
            pdfStream.CopyTo(fs);
        }
        try
        {
            var printTimeout = new TimeSpan(0, 30, 0);
            printer.Print(new PrintingOptions(input.PrinterName, tempPath), printTimeout);
        }
        finally
        {
            File.Delete(tempPath);
        }

        return Task.CompletedTask;
    }
}