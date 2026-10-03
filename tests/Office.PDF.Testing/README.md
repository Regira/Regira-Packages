# Office.PDF.Testing

Tests for the PDF backends: [PDF.DocNET](../../src/PDF.DocNET/README.md) (text, merge, split, page images,
images to PDF, empty-page removal), [PDF.Spire](../../src/PDF.Spire/README.md) (split, merge, page images), and
HTML to PDF through [PDF.SelectPdf](../../src/PDF.SelectPdf/README.md),
[PDF.Playwright](../../src/PDF.Playwright/README.md) and [PDF.Puppeteer](../../src/PDF.Puppeteer/README.md). NUnit.

## Running

```bash
dotnet test tests/Office.PDF.Testing
```

- `PlaywrightTests` and `PuppeteerTests` download Chromium the first time they run, so that run needs network
  access. They have no test category, so `TestCategory!=Network` does not skip them; use
  `--filter "FullyQualifiedName!~PlaywrightTests&FullyQualifiedName!~PuppeteerTests"`.
- `SpirePdfTests` needs Windows: PDF.Spire renders through GDI+. The fixture has no platform guard.

No secrets. Inputs are in `Assets/Input`; each backend writes to `Assets/Output/{Backend}`.
