using Regira.IO.Abstractions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Internal;
using Regira.Office.PDF.Models;
using Microsoft.Playwright;
using Regira.IO.Extensions;

namespace Regira.Office.PDF.MsPlaywright;

/// <summary>
/// Renders HTML to PDF in headless Chromium. Every <see cref="HtmlInput"/> setting is applied: the page size and
/// orientation, the margins, and the header and footer, which Chromium draws in a band inside the top and bottom margin.
/// </summary>
public class PdfManager : IHtmlToPdfService
{
    private static readonly SemaphoreSlim InstallLock = new(1, 1);
    private static bool _browserInstalled;

    public async Task<IMemoryFile> Create(HtmlInput template, CancellationToken cancellationToken = default)
    {
        await InstallLock.WaitAsync(cancellationToken);
        try
        {
            if (!_browserInstalled)
            {
                var exitCode = Program.Main(["install", "chromium"]);
                if (exitCode != 0)
                {
                    throw new InvalidOperationException($"Playwright browser installation failed with exit code {exitCode}");
                }
                _browserInstalled = true;
            }
        }
        finally
        {
            InstallLock.Release();
        }

        using var playwright = await Playwright.CreateAsync();
        await using var browser = await playwright.Chromium.LaunchAsync(new BrowserTypeLaunchOptions { Headless = true });
        var page = await browser.NewPageAsync();
        await page.SetContentAsync(template.HtmlContent ?? string.Empty);
        var layout = ChromiumPdfLayout.From(template);
        var bytes = await page.PdfAsync(new PagePdfOptions
        {
            Width = layout.Width,
            Height = layout.Height,
            Margin = new Margin
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
        return bytes.ToMemoryFile();
    }
}