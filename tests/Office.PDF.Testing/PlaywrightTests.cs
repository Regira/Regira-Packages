using Office.PDF.Testing.Abstractions;
using Regira.Office.PDF.MsPlaywright;

namespace Office.PDF.Testing;

/// <summary>PDF.MsPlaywright runs every shared scenario in <see cref="HtmlToPdfTestsBase"/>, in a Chromium it installs on first use.</summary>
[TestFixture]
[Category("Browser")]
public class PlaywrightTests() : HtmlToPdfTestsBase(new PdfManager(), "Playwright")
{
}
