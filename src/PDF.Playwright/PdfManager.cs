using Regira.IO.Abstractions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Models;
using Microsoft.Playwright;
using Regira.IO.Extensions;

namespace Regira.Office.PDF.MsPlaywright;

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
        var bytes = await page.PdfAsync(new PagePdfOptions { Format = "A4" });
        return bytes.ToMemoryFile();
    }
}