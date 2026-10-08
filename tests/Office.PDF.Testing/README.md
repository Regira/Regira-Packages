# Office.PDF.Testing

Tests for the PDF backends: [PDF.PdfPig](../../src/PDF.PdfPig/README.md), [PDF.DocNET](../../src/PDF.DocNET/README.md)
and [PDF.Spire](../../src/PDF.Spire/README.md) for PDF operations, [PDF.MiniPdf](../../src/PDF.MiniPdf/README.md) for
Word, Excel and PowerPoint to PDF, and [PDF.SelectPdf](../../src/PDF.SelectPdf/README.md),
[PDF.Playwright](../../src/PDF.Playwright/README.md) and [PDF.Puppeteer](../../src/PDF.Puppeteer/README.md) for HTML to
PDF. NUnit.

## How the suite is built

- **Shared scenarios.** `Abstractions/PdfTestsBase` holds the scenarios of the PDF operations: text, merging,
  splitting, removing pages, page images and images to PDF, edges included — merging nothing, a split range outside the
  document, removing every page. A backend fixture is marked `[PdfFixture]` and gets every scenario whose `[Needs]` its
  backend type implements, so Spire, which has no `IPdfEditor`, gets no page removal; a scenario a backend cannot run
  does not appear in the results. A fixture declares the limits of a free edition (`MaxPages`, `RenderedPages`) and
  overrides a scenario only where its backend documents another behaviour: DocNET's page per image.
  `Abstractions/HtmlToPdfTestsBase` holds the HTML-to-PDF scenarios every `IHtmlToPdfService` runs: page size,
  orientation, margins, header and footer placement, and a template filled with parameters.
- **Measured results.** A scenario reads what it produced with `PdfFacts` or `ImageFacts` (`Abstractions/Facts`): the
  PdfPig library and SkiaSharp, never the backend under test. It compares each page's text and size with the input page
  it came from, places words and images on the page, and checks a rendered page's size, format (from its bytes) and
  ink. A page's text holds only the words that lie on it: SelectPdf draws the whole document on every page and clips
  it.
- **Outputs.** Every file a scenario produces is saved as `Assets/Output/{Backend}/{test name}`, with the facts it was
  read for beside it as `{file}.facts.json`, and attached to the test result. `OutputSetUp` empties `Assets/Output` at
  the start of a run. Diff two runs' facts to see what a vendor upgrade or a change moved.

`SpireTests` pins the limits of FreeSpire.PDF's free edition — ten pages to a PDF, three rendered — so an upgrade
that moves them fails there. `MiniPdfTests` covers the only `IDocumentToPdfService`.

## Running

```bash
dotnet test tests/Office.PDF.Testing
```

- `Browser`: `PlaywrightTests` and `PuppeteerTests` download Chromium the first time they run, so that run needs
  network access; each PDF takes one to three seconds. Skip them with `--filter "TestCategory!=Browser"`.
- `SpireTests` and `SelectPdfTests` need Windows: both render through GDI+. The fixtures have no platform guard.

No secrets. Inputs are in `Assets/Input`.
