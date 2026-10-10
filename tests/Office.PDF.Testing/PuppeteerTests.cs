using Office.PDF.Testing.Abstractions;
using Regira.Office.PDF.Puppeteer;

namespace Office.PDF.Testing;

/// <summary>PDF.Puppeteer runs every shared scenario in <see cref="HtmlToPdfTestsBase"/>, in a Chromium it downloads on first use.</summary>
[TestFixture]
[Category("Browser")]
public class PuppeteerTests() : HtmlToPdfTestsBase(new PdfManager(), "Puppeteer")
{
}
