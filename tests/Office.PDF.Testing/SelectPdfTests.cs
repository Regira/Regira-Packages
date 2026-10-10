using Office.PDF.Testing.Abstractions;
using Regira.Office.PDF.SelectPdf;

namespace Office.PDF.Testing;

/// <summary>PDF.SelectPdf runs every shared scenario in <see cref="HtmlToPdfTestsBase"/>.</summary>
[TestFixture]
public class SelectPdfTests() : HtmlToPdfTestsBase(new PdfManager(), "SelectPdf")
{
}
