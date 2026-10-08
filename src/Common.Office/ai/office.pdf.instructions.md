# Regira Office.PDF AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.PDF` | HTML→PDF, Office documents (Word, Excel, PowerPoint)→PDF, PDF operations (merge/split/extract), printing |

**Related:**
- **Media / Drawing** — `IImageService` required by `PdfPig.PdfService` (and `DocNET.PdfManager`); `IImageFile` ↔ PDF conversion. `get_package(id: "Regira.Media", section: "media.instructions")`, or `media.instructions.md` locally.
- **IO.Storage** — `IMemoryFile` used for PDF input/output. `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`, or `io.storage.instructions.md` locally.

---

## Installation

```xml
<!-- HTML→PDF (recommended — full options support) -->
<PackageReference Include="Regira.Office.PDF.SelectPdf" Version="6.*" />

<!-- HTML→PDF (headless Chromium) -->
<PackageReference Include="Regira.Office.PDF.Puppeteer" Version="6.*" />
<PackageReference Include="Regira.Office.PDF.MsPlaywright" Version="6.*" />

<!-- Word, Excel and PowerPoint → PDF, in-process, no licence -->
<PackageReference Include="Regira.Office.PDF.MiniPdf" Version="6.*" />

<!-- PDF operations (merge, split, text, images) — recommended -->
<PackageReference Include="Regira.Office.PDF.PdfPig" Version="6.*" />

<!-- PDF operations on Docnet.Core — deprecated -->
<PackageReference Include="Regira.Office.PDF.DocNET" Version="6.*" />

<!-- PDF operations + printing -->
<PackageReference Include="Regira.Office.PDF.Spire" Version="6.*" />

<!-- Print (Windows) -->
<PackageReference Include="Regira.Office.PDF.PDFtoPrinter" Version="6.*" />
<PackageReference Include="Regira.Office.PDF.PockyBum522" Version="6.*" />
```

---

## Backend Comparison

| Package | Backend | HTML→PDF | Office→PDF | PDF Ops | Print | Runtime footprint |
|---|---|---|---|---|---|---|
| `PDF.SelectPdf` | Select.HtmlToPdf | ✓ full | — | — | — | Pulls `System.Drawing.Common`, which throws on non-Windows from .NET 6 on — treat as **Windows**. The free Community Edition converts only the first **five pages'** worth of a document and drops the rest without an error or a notice |
| `PDF.Puppeteer` | PuppeteerSharp | ✓ A4 | — | — | — | **Downloads Chromium on first use** (`BrowserFetcher().DownloadAsync()`) — needs network + disk at runtime, or a pre-seeded cache |
| `PDF.MsPlaywright` | Microsoft.Playwright | ✓ A4 | — | — | — | **Installs its browser on first use** — same constraint; the install is guarded by a process-wide lock, so the first request pays for it |
| `PDF.PdfPig` | PdfPig + PDFtoImage | — | — | merge, split, img↔pdf, text | — | PdfPig is fully managed; page images render through PDFtoImage over PDFium, which ships native binaries for Windows, Linux and macOS (x64 and arm64). On Linux it carries SkiaSharp's dependency-free native library; an application that adds `SkiaSharp.NativeAssets.Linux` itself gets that one instead, which needs `libfontconfig1` |
| `PDF.DocNET` | Docnet.Core | — | — | merge, split, img↔pdf, text | — | **Deprecated.** Docnet.Core has had no release since 2.6.0 (2023) and bundles a PDFium build from 2022, which parses every PDF it is given — a risk for uploaded files. Managed wrapper over a native library — the RID must be one `Docnet.Core` ships binaries for |
| `PDF.MiniPdf` | MiniPdf | — | ✓ docx, xlsx, pptx | — | — | Managed and in-process: no Office, server or browser. MiniPdf lays documents out itself, so a complex layout comes out less faithful than through LibreOffice or Word. Text renders in the host's system fonts — on a host with few (a container), register TrueType fonts once at startup with `MiniSoftware.MiniPdf.RegisterFont`, a registration for the whole process |
| `PDF.Spire` | FreeSpire.PDF | — | — | merge, split, img, text | ✓ | The **free** edition: loading or creating a PDF of more than **ten pages** throws (a merge whose result passes ten included), and `ToImages` renders only the first **three** pages, returning blank images for the rest |
| `PDF.PDFtoPrinter` | PDFtoPrinter | — | — | — | ✓ (Win) | Drives an external printing utility |
| `PDF.PockyBum522` | SimpleFreePdfPrinter | — | — | — | ✓ (Win) | Targets `net*-windows` — **will not build** on a non-Windows TFM |

**Recommendations:**
- HTML → PDF: **SelectPdf** on Windows for documents of up to five pages (full options, nothing to download);
  **Puppeteer**/**Playwright** where the host is Linux, a document runs longer, or the CSS must be pixel-perfect,
  and a first-run browser fetch is acceptable
- Office documents → PDF: **MiniPdf** — `.docx`, `.xlsx` and `.pptx`, in-process, no licence and no server. For a
  Word document that must lay out as Word does, convert it with a Word backend (`IWordConverter`) instead
- PDF operations: **PdfPig** (merge, split, page removal, images, text extraction) — cross-platform, no licence
- Printing: **Spire** (operations + print) or **PDFtoPrinter** (print-only, Windows)

**PdfPig behaviour** (`Regira.Office.PDF.PdfPig.PdfService`, constructed with an `IImageService`):
- `ImagesToPdf` puts each image on a page of its own, of the input's `Format` and `Orientation` (A4 portrait by
  default), centred horizontally between the `Margins` and starting at the top margin, at one pixel per `DPI` unit and
  scaled down when it does not fit — never up.
  JPEG and PNG are embedded as they are, any other format as PNG. No images gives `null`.
- `ToImages` fits each page to `PdfToImagesOptions.Size` either way round — the page's shorter side to the smaller
  dimension, its longer side to the larger — keeping the aspect ratio. Annotations and filled-in form fields are drawn.
- `GetText` reads the text in the order the PDF draws it. A scanned page holds no text, so `RemoveEmptyPages`
  removes it.

---

## Interfaces

### `IHtmlToPdfService`

<!-- no-compile -->
```csharp
Task<IMemoryFile> Create(HtmlInput input, CancellationToken cancellationToken = default);
```

### `IDocumentToPdfService`

<!-- no-compile -->
```csharp
Task<IMemoryFile> Create(DocumentInput input, CancellationToken cancellationToken = default);
```

Converts a Word document, a spreadsheet or a presentation. A source format or a `DocumentInput` setting the
backend cannot handle throws `NotSupportedException`; a setting is never ignored silently.

### `IPdfMerger`

<!-- no-compile -->
```csharp
Task<IMemoryFile?>             Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default);
```

### `IPdfSplitter`

<!-- no-compile -->
```csharp
Task<IEnumerable<IMemoryFile>>  Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default);
Task<int>                       GetPageCount(IMemoryFile pdf, CancellationToken cancellationToken = default);
```

### `IPdfEditor` (extends `IPdfMerger` + `IPdfSplitter`)

<!-- no-compile -->
```csharp
Task<IMemoryFile?>  RemovePages(IMemoryFile pdf, IEnumerable<int> pages, CancellationToken cancellationToken = default);
```

### `IPdfToImageService` / `IImagesToPdfService`

<!-- no-compile -->
```csharp
Task<IList<IImageFile>>  ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default);
Task<IMemoryFile?>       ImagesToPdf(ImagesInput input, CancellationToken cancellationToken = default);
```

### `IPdfToImageAsyncService`

<!-- no-compile -->
```csharp
IAsyncEnumerable<IImageFile>  ToImagesAsync(IMemoryFile pdf, PdfToImagesOptions? options = null);
```

### `IPdfTextExtractor`

<!-- no-compile -->
```csharp
Task<string>          GetText(IMemoryFile pdf, CancellationToken cancellationToken = default);
```

### `IPdfTextService` (extends `IPdfTextExtractor`)

<!-- no-compile -->
```csharp
Task<IList<string>>   GetTextPerPage(IMemoryFile pdf, CancellationToken cancellationToken = default);
Task<IMemoryFile?>    RemoveEmptyPages(IMemoryFile pdf, CancellationToken cancellationToken = default);
```

### `IPdfPrinter`

<!-- no-compile -->
```csharp
string              DefaultPrinter { get; }
Task<IList<string>> List(CancellationToken cancellationToken = default);
Task                Print(PdfPrinterInput input, CancellationToken cancellationToken = default);
```

### `IPdfService`

Composite: `IPdfEditor + IPdfImageService + IPdfTextService`. Implemented by `PDF.PdfPig.PdfService` (and the deprecated `PDF.DocNET.PdfManager`). `PDF.Spire.PdfManager` implements `IPdfMerger`, `IPdfSplitter`, `IPdfToImageService` and `IPdfTextExtractor` only — resolve those, not `IPdfService`.

---

## Models

### `HtmlInput`

| Property | Type | Default | Description |
|---|---|---|---|
| `HtmlContent` | `string?` | `null` | HTML to convert |
| `HeaderHtmlContent` | `string?` | `null` | Repeating page header |
| `FooterHtmlContent` | `string?` | `null` | Repeating page footer |
| `HeaderHeight` | `int?` | `null` | Header height in mm |
| `FooterHeight` | `int?` | `null` | Footer height in mm |
| `Format` | `PageSize` | `A4` | Paper size |
| `Orientation` | `PageOrientation` | `Portrait` | Portrait / Landscape |
| `Margins` | `Margins` | `10mm` all | Page margins (in points) |
| `DPI` | `int` | `96` | Render resolution |

### `DocumentInput`

| Property | Type | Default | Description |
|---|---|---|---|
| `Document` | `IMemoryFile` | *(required)* | Document to convert |
| `Format` | `PageSize?` | `null` | Paper size; `null` keeps the document's own |
| `Orientation` | `PageOrientation?` | `null` | Portrait / Landscape; `null` keeps the document's own |
| `Margins` | `Margins?` | `null` | Page margins in points; `null` keeps the document's own |

MiniPdf applies them per source format:

| Source | Takes | Throws `NotSupportedException` for |
|---|---|---|
| `.docx` | `Format` (with or without `Orientation`), `Margins` | `Orientation` without `Format` — MiniPdf replaces the page size as a whole |
| `.xlsx` | `Orientation` | `Format`, `Margins` |
| `.pptx` | — (a slide keeps the presentation's slide size) | `Format`, `Orientation`, `Margins` |

Any other source — `.doc`, `.xls`, `.odt`, `.rtf`, a PDF — throws `NotSupportedException`. Every visible sheet of a
workbook is rendered, in order.

### `PdfSplitRange`

| Property | Type | Description |
|---|---|---|
| `Start` | `int` | First page (1-indexed) |
| `End` | `int?` | Last page (`null` = last page of document) |

### `PdfToImagesOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `Size` | `ImageSize?` | `1080 × 1920` | Output image dimensions |
| `Format` | `ImageFormat` | `Jpeg` | Output image format |

### `PdfPrinterInput`

| Property | Type | Default | Description |
|---|---|---|---|
| `PdfFile` | `IMemoryFile` | *(required)* | PDF to print |
| `PrinterName` | `string?` | default printer | Target printer |
| `PageSize` | `PageSize` | `A4` | Paper size |
| `PageOrientation` | `PageOrientation` | `Portrait` | Orientation |

---

## Usage

<!-- no-compile -->
```csharp
// HTML → PDF (SelectPdf)
IHtmlToPdfService pdf = new Regira.Office.PDF.SelectPdf.PdfManager();
IMemoryFile file = await pdf.Create(new HtmlInput
{
    HtmlContent = "<h1>Invoice</h1>",
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

// Merge PDFs (PdfPig)
IPdfMerger merger = new Regira.Office.PDF.PdfPig.PdfService(imageService);
IMemoryFile merged = (await merger.Merge([pdf1, pdf2, pdf3]))!;

// Extract text (PdfPig)
IPdfTextExtractor extractor = new Regira.Office.PDF.PdfPig.PdfService(imageService);
string text = await extractor.GetText(pdfFile);

// Reading a result: whether it carries bytes or a stream depends on the method, so read it with
// GetBytes() (Regira.IO.Extensions, in Regira.Common), which normalises both shapes.
byte[] bytes = file.GetBytes()!;
```

⚠️ **Read the result with `GetBytes()`, never `.Bytes` directly.** `IMemoryFile` extends both
`IMemoryBytesFile` (`Bytes`) and `IMemoryStreamFile` (`Stream`), which makes `.Bytes` look like the obvious
accessor — but a producer fills exactly one of them, and which one is a property of the **method**, not of
the backend you picked. `Create` returns a stream-backed file on SelectPdf and Puppeteer and a byte-backed
one on Playwright; DocNET's `Split` returns bytes where its `Merge(IEnumerable<IMemoryFile>)` returns a
stream; Spire is stream-backed throughout `Merge`/`Split`. So there is nothing to check at the call site,
and `.Bytes` is simply null for half the API — producing a **200 with an empty body**: no exception, no
log, and a download that opens as a zero-byte file. `GetBytes()` returns the bytes whichever half is
populated.

---
