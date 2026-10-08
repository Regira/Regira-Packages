using PuppeteerSharp;
using PuppeteerSharp.Media;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Internal;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Puppeteer;

/// <summary>
/// Renders HTML to PDF in headless Chromium. Every <see cref="HtmlInput"/> setting is applied: the page size and
/// orientation, the margins, and the header and footer, which Chromium draws in a band inside the top and bottom margin.
/// </summary>
public class PdfManager : IHtmlToPdfService
{
    private static readonly SemaphoreSlim DownloadLock = new(1, 1);

    public async Task<IMemoryFile> Create(HtmlInput template, CancellationToken cancellationToken = default)
    {
        await DownloadLock.WaitAsync(cancellationToken);
        try
        {
            await new BrowserFetcher().DownloadAsync();
        }
        finally
        {
            DownloadLock.Release();
        }

        // Dispose the browser (and page) before returning: PdfStreamAsync buffers the whole PDF
        // into a self-contained MemoryStream, so it stays readable once the browser is gone.
        // Without this, every call leaks a chrome.exe process tree that keeps chrome.dll locked.
        await using var browser = await PuppeteerSharp.Puppeteer.LaunchAsync(new LaunchOptions
        {
            Headless = true
        });
        await using var page = await browser.NewPageAsync();
        await page.SetContentAsync(template.HtmlContent);

        var layout = ChromiumPdfLayout.From(template);
        var pdfStream = await page.PdfStreamAsync(new PdfOptions
        {
            Width = layout.Width,
            Height = layout.Height,
            MarginOptions = new MarginOptions
            {
                Top = layout.MarginTop,
                Right = layout.MarginRight,
                Bottom = layout.MarginBottom,
                Left = layout.MarginLeft
            },
            DisplayHeaderFooter = layout.DisplayHeaderFooter,
            HeaderTemplate = layout.HeaderTemplate,
            FooterTemplate = layout.FooterTemplate
        });

        return pdfStream.ToMemoryFile();
    }
}