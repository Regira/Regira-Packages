# Regira Office.PDF

Regira Office.PDF provides a **unified abstraction** for PDF operations — HTML→PDF, Office documents→PDF, images→PDF, PDF→images, text extraction, merge/split, and printing — across multiple underlying libraries.

## Projects

| Project | Package | Backend | HTML→PDF | Office→PDF | PDF ops | Print |
|---------|---------|---------|----------|------------|---------|-------|
| `Common.Office` | *(transitive)* | Shared abstractions | — | — | — | — |
| `PDF.SelectPdf` | `Regira.Office.PDF.SelectPdf` | Select.HtmlToPdf | ✓ full | — | — | — |
| `PDF.Puppeteer` | `Regira.Office.PDF.Puppeteer` | PuppeteerSharp | ✓ full | — | — | — |
| `PDF.Playwright` | `Regira.Office.PDF.MsPlaywright` | Microsoft.Playwright | ✓ full | — | — | — |
| `PDF.PdfPig` | `Regira.Office.PDF.PdfPig` | PdfPig + PDFtoImage | — | — | merge, split, img↔pdf, text | — |
| `PDF.DocNET` | `Regira.Office.PDF.DocNET` | Docnet.Core (deprecated) | — | — | merge, split, img↔pdf, text | — |
| `PDF.MiniPdf` | `Regira.Office.PDF.MiniPdf` | MiniPdf | — | ✓ docx, xlsx, pptx | — | — |
| `PDF.Spire` | `Regira.Office.PDF.Spire` | FreeSpire.PDF | — | — | merge, split, pdf→img, text | ✓ |
| `PDF.PDFtoPrinter` | `Regira.Office.PDF.PDFtoPrinter` | PDFtoPrinter | — | — | — | ✓ (Win) |
| `PDF.PockyBum522` | `Regira.Office.PDF.PockyBum522` | SimpleFreePdfPrinter | — | — | — | ✓ (Win) |

## Installation

```xml
<!-- HTML→PDF (recommended — headless Chromium, any OS) -->
<PackageReference Include="Regira.Office.PDF.MsPlaywright" Version="6.*" />
<PackageReference Include="Regira.Office.PDF.Puppeteer" Version="6.*" />

<!-- HTML→PDF (Windows, up to five pages, nothing to download) -->
<PackageReference Include="Regira.Office.PDF.SelectPdf" Version="6.*" />

<!-- Word, Excel and PowerPoint → PDF (in-process, no licence) -->
<PackageReference Include="Regira.Office.PDF.MiniPdf" Version="6.*" />

<!-- PDF operations (merge, split, text, images) -->
<PackageReference Include="Regira.Office.PDF.PdfPig" Version="6.*" />
<PackageReference Include="Regira.Office.PDF.Spire" Version="6.*" />

<!-- PDF operations on Docnet.Core (deprecated) -->
<PackageReference Include="Regira.Office.PDF.DocNET" Version="6.*" />

<!-- Print (Windows) -->
<PackageReference Include="Regira.Office.PDF.PDFtoPrinter" Version="6.*" />
<PackageReference Include="Regira.Office.PDF.PockyBum522" Version="6.*" />
```

## Quick Start

```csharp
// HTML → PDF (Playwright)
IHtmlToPdfService pdf = new Regira.Office.PDF.MsPlaywright.PdfManager();
IMemoryFile file = await pdf.Create(new HtmlInput
{
    HtmlContent = "<h1>Hello</h1>",
    Format      = PageSize.A4,
    Orientation = PageOrientation.Portrait
});

// Word, Excel or PowerPoint → PDF (MiniPdf)
IDocumentToPdfService converter = new Regira.Office.PDF.MiniPdf.PdfService();
IMemoryFile reportPdf = await converter.Create(new DocumentInput
{
    Document = File.ReadAllBytes("report.docx").ToMemoryFile(),
    Format   = PageSize.A4
});

// Merge PDFs (PdfPig — needs an IImageService for the image-related operations)
IMemoryFile pdf1 = File.ReadAllBytes("1.pdf").ToMemoryFile();
IMemoryFile pdf2 = File.ReadAllBytes("2.pdf").ToMemoryFile();
IMemoryFile pdf3 = File.ReadAllBytes("3.pdf").ToMemoryFile();
IImageService imageService = new Regira.Drawing.SkiaSharp.Services.ImageService();
IPdfMerger merger = new Regira.Office.PDF.PdfPig.PdfService(imageService);
IMemoryFile merged = (await merger.Merge([pdf1, pdf2, pdf3]))!;

// Reading a result — GetBytes() (Regira.IO.Extensions), not .Bytes
byte[] bytes = merged.GetBytes()!;
```

`IMemoryFile` extends both `IMemoryBytesFile` (`Bytes`) and `IMemoryStreamFile` (`Stream`), and a producer
fills exactly one. Which one varies per method rather than per backend — DocNET's `Split` returns
byte-backed files while its `Merge` returns a stream-backed one — so `.Bytes` reads `null` for half the
API and yields an empty file with no exception. `GetBytes()` normalises both and is correct everywhere.

## Interfaces

### IHtmlToPdfService

<!-- no-compile -->
```csharp
Task<IMemoryFile> Create(HtmlInput template, CancellationToken cancellationToken = default);
```

### IDocumentToPdfService

<!-- no-compile -->
```csharp
Task<IMemoryFile> Create(DocumentInput input, CancellationToken cancellationToken = default);
```

Converts a Word document, a spreadsheet or a presentation. A source format or a `DocumentInput` setting the
backend cannot handle throws `NotSupportedException` rather than being ignored.

### IPdfMerger / IPdfSplitter / IPdfEditor

<!-- no-compile -->
```csharp
// IPdfMerger
Task<IMemoryFile?>              Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default);

// IPdfSplitter
Task<IEnumerable<IMemoryFile>>  Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default);
Task<int>                       GetPageCount(IMemoryFile pdf, CancellationToken cancellationToken = default);

// IPdfEditor : IPdfMerger, IPdfSplitter
Task<IMemoryFile?>              RemovePages(IMemoryFile pdf, IEnumerable<int> pages, CancellationToken cancellationToken = default);
```

### IPdfToImageService / IImagesToPdfService

<!-- no-compile -->
```csharp
Task<IList<IImageFile>>  ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default);
Task<IMemoryFile?>       ImagesToPdf(ImagesInput input, CancellationToken cancellationToken = default);
```

### IPdfTextExtractor / IPdfTextService

<!-- no-compile -->
```csharp
Task<string>          GetText(IMemoryFile pdf, CancellationToken cancellationToken = default);
Task<IList<string>>   GetTextPerPage(IMemoryFile pdf, CancellationToken cancellationToken = default);
Task<IMemoryFile?>    RemoveEmptyPages(IMemoryFile pdf, CancellationToken cancellationToken = default);
```

### IPdfPrinter

<!-- no-compile -->
```csharp
string              DefaultPrinter { get; }
Task<IList<string>> List(CancellationToken cancellationToken = default);
Task                Print(PdfPrinterInput input, CancellationToken cancellationToken = default);
```

### IPdfService

Composite: `IPdfEditor + IPdfImageService + IPdfTextService`. Implemented by PdfPig's `PdfService` (and the deprecated DocNET `PdfManager`).

## Input / Output Models

### HtmlInput

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `HtmlContent` | `string?` | `null` | HTML to convert |
| `HeaderHtmlContent` | `string?` | `null` | Repeating page header |
| `FooterHtmlContent` | `string?` | `null` | Repeating page footer |
| `HeaderHeight` | `int?` | `null` | Header height in mm; `null` is 45 pt |
| `FooterHeight` | `int?` | `null` | Footer height in mm; `null` is 35 pt |
| `Format` | `PageSize` | `A4` | Paper size |
| `Orientation` | `PageOrientation` | `Portrait` | Portrait / Landscape |
| `Margins` | `Margins` | `10mm` all | Page margins, in units of `DPI`; `Margins.ModifyDpi` measures them at another |
| `DPI` | `int` | `96` | Units per inch of `Margins` |

### DocumentInput

The page settings override the document's own page setup; each one left `null` keeps what the document says.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Document` | `IMemoryFile` | *(required)* | Document to convert |
| `Format` | `PageSize?` | `null` | Paper size |
| `Orientation` | `PageOrientation?` | `null` | Portrait / Landscape |
| `Margins` | `Margins?` | `null` | Page margins (in points) |

### ImagesInput

Same base properties as `HtmlInput` plus:

| Property | Type | Description |
|----------|------|-------------|
| `Images` | `ICollection<byte[]>` | One image per page |

### PdfToImagesOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Size` | `ImageSize?` | `1080 × 1920` | Output image dimensions |
| `Format` | `ImageFormat` | `Jpeg` | Output image format |

### PdfSplitRange

| Property | Type | Description |
|----------|------|-------------|
| `Start` | `int` | First page (1-indexed) |
| `End` | `int?` | Last page (`null` = last page of document) |

### PdfPrinterInput

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PdfFile` | `IMemoryFile` | *(required)* | PDF to print |
| `PrinterName` | `string?` | default printer | Target printer |
| `PageSize` | `PageSize` | `A4` | Paper size |
| `PageOrientation` | `PageOrientation` | `Portrait` | Orientation |

## Implementation notes

### Playwright / Puppeteer — headless Chromium, recommended for HTML→PDF

Both render in headless Chromium and apply every `HtmlInput` setting, on Windows, Linux and macOS, with no page limit;
Playwright is the recommended one. Chromium is installed (Playwright) or downloaded (Puppeteer) on first use, guarded
by a process-wide lock, so the first conversion needs network access and writable disk, or a browser installed in
advance. Each conversion starts a browser of its own, which takes one to three seconds.

- **Page.** `Format` and `Orientation` set the paper, A0 to A10; the CSS `@page` size is not used.
- **Header and footer.** The header takes a band of `HeaderHeight` below the top margin and the footer a band of
  `FooterHeight` above the bottom margin, both between the left and right margins; the body starts below the header's
  band and ends above the footer's. Chromium renders each on its own: the page's stylesheets do not reach it and it
  loads nothing by URL, so give it inline styles and images as `data:` URIs. Its text starts at 16px. An element with
  the class `pageNumber` or `totalPages` gets the page number or the page count:

  ```html
  <div style="text-align:center">Page <span class="pageNumber"></span> of <span class="totalPages"></span></div>
  ```
- **Backgrounds.** Chromium prints as a printer would: CSS background colours and images are left out.

### SelectPdf — HTML→PDF on Windows, up to five pages

Applies every `HtmlInput` property: page size, orientation, margins, headers, footers. Does not require a browser
installation.

It runs on Windows only: it renders through `System.Drawing.Common`, which throws on other platforms. The free
Community Edition of Select.HtmlToPdf converts only the first five pages' worth of a document, and leaves the rest out
of the PDF without an error or a notice. For longer documents, use the vendor's paid edition or Playwright.

It renders a header or footer as a page of its own, with the browser's default 8px around its body, and shows no page
numbers in it. Every page holds the whole document, clipped to its own part, so text extracted from one page is the
whole document's.

### MiniPdf — Word, Excel and PowerPoint to PDF

`PdfService` implements `IDocumentToPdfService` with [MiniPdf](https://github.com/mini-software/MiniPdf): it
converts `.docx`, `.xlsx` and `.pptx` in-process, with no Office installation, server, browser or licence. MiniPdf
lays documents out itself, so a complex layout comes out less faithful than through LibreOffice or Word; for a Word
document that must match Word's layout, convert it with a Word backend's `IWordConverter`.

It applies the `DocumentInput` settings per source format:

| Source | Takes | Throws `NotSupportedException` for |
|--------|-------|-------------------------------------|
| `.docx` | `Format` (with or without `Orientation`), `Margins` | `Orientation` without `Format` — MiniPdf replaces the page size as a whole |
| `.xlsx` | `Orientation` | `Format`, `Margins` |
| `.pptx` | — (a slide keeps the presentation's slide size) | `Format`, `Orientation`, `Margins` |

Any other source — `.doc`, `.xls`, `.odt`, `.rtf`, a PDF — throws `NotSupportedException`. Every visible sheet of
a workbook is rendered, in order.

Text renders in the host's system fonts. On a host with few — a container — register TrueType fonts once at
startup; the registration holds for the whole process:

<!-- no-compile -->
```csharp
MiniSoftware.MiniPdf.RegisterFont("NotoSans", File.ReadAllBytes("Fonts/NotoSans-Regular.ttf"));
```

### PdfPig — recommended for PDF operations

`PdfService` implements `IPdfService`: merge, split, page removal, text extraction, and images↔PDF. Merging,
splitting, text and images→PDF run on [PdfPig](https://github.com/UglyToad/PdfPig), which is fully managed; page
images render through [PDFtoImage](https://github.com/sungaila/PDFtoImage) over PDFium, which ships native binaries
for Windows, Linux and macOS (x64 and arm64). Both are free for commercial use (Apache-2.0 and MIT). The constructor
takes an `IImageService`, which reads the images going into a PDF and produces the page images coming out of one.

```csharp
IImageService imageService = new Regira.Drawing.SkiaSharp.Services.ImageService();
var pdf = new Regira.Office.PDF.PdfPig.PdfService(imageService);
```

- **Images → PDF.** Each image gets a page of its own, of the input's `Format` and `Orientation` (A4 portrait by
  default). The image is centred horizontally between the `Margins` and starts at the top margin, at one pixel per
  `DPI` unit, scaled down when it does not fit — never up. JPEG and PNG are embedded as they are, any other format as PNG. An input without images gives
  `null`.
- **PDF → images.** Each page is fitted to `PdfToImagesOptions.Size` either way round — the page's shorter side to
  the smaller dimension, its longer side to the larger — keeping its aspect ratio, so the default `1080 × 1920` gives
  a portrait A4 page 1080 pixels wide and a landscape one 1080 pixels high. Annotations and filled-in form fields
  are drawn, as a PDF viewer shows them.
- **Linux.** The package carries SkiaSharp's dependency-free Linux native library, at the SkiaSharp version
  Regira.Drawing.SkiaSharp uses, so nothing needs installing. An application that adds `SkiaSharp.NativeAssets.Linux`
  itself, as Regira.Drawing.SkiaSharp describes, gets that library instead, which needs `libfontconfig1` on the host.
- **Text.** `GetText` and `GetTextPerPage` read the text in the order the PDF draws it. A scanned page holds images
  and no text, so `RemoveEmptyPages` removes it.

### DocNET — deprecated

Implements `IPdfService` on [Docnet.Core](https://www.nuget.org/packages/Docnet.Core), which has had no release since
2.6.0 (2023) and bundles a PDFium build from 2022. PDFium parses every PDF it is given, so an outdated build is a risk
for uploaded files; use PdfPig. `ImagesToPdf` makes each page the size of its image and ignores the page format and
margins. Requires `IImageService` in the constructor.

### Spire — PDF operations + printing

Implements `IPdfMerger`, `IPdfSplitter`, `IPdfToImageService` and `IPdfTextExtractor` — not the full `IPdfService`: there is no `RemovePages`, `ImagesToPdf`, `GetTextPerPage` or `RemoveEmptyPages`, and image conversion is PDF→image only. Also ships `PdfPrinter` for Windows printing with page size override support.

FreeSpire.PDF is the vendor's free edition. Loading or creating a PDF of more than ten pages throws, a merge whose result passes ten included, and `ToImages` renders only the first three pages, returning blank images for the rest.

## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — HTML→PDF, Office documents→PDF, merge, split, text extraction, printing
