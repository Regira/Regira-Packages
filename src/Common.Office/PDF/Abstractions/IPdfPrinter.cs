using Regira.Office.PDF.Printer;

namespace Regira.Office.PDF.Abstractions;

public interface IPdfPrinter
{
    string DefaultPrinter { get; }

    Task<IList<string>> List(CancellationToken cancellationToken = default);
    Task Print(PdfPrinterInput input, CancellationToken cancellationToken = default);
}