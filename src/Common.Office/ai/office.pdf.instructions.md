# Regira Office.PDF AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.PDF` | HTML→PDF, PDF operations (merge/split/extract), printing |

**Related:**
- [Media / Drawing](../../Common.Media/ai/media.instructions.md) — `IImageService` required by `DocNET.PdfManager`; `IImageFile` ↔ PDF conversion
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `IMemoryFile` used for PDF input/output

---

## Installation

```xml
<!-- HTML→PDF (recommended — full options support) -->
<PackageReference Include="Regira.Office.PDF.SelectPdf" Version="6.*" />

<!-- HTML→PDF (headless Chromium) -->
<PackageReference Include="Regira.Office.PDF.Puppeteer" Version="6.*" />
<PackageReference Include="Regira.Office.PDF.MsPlaywright" Version="6.*" />

<!-- PDF operations (merge, split, text, images) — recommended -->
<PackageReference Include="Regira.Office.PDF.DocNET" Version="6.*" />

<!-- PDF operations + printing -->
<PackageReference Include="Regira.Office.PDF.Spire" Version="6.*" />

<!-- Print (Windows) -->
<PackageReference Include="Regira.Office.PDF.PDFtoPrinter" Version="6.*" />
<PackageReference Include="Regira.Office.PDF.PockyBum522" Version="6.*" />
```

---

## Backend Comparison

| Package | Backend | HTML→PDF | PDF Ops | Print | Runtime footprint |
|---|---|---|---|---|---|
| `PDF.SelectPdf` | Select.HtmlToPdf | ✓ full | — | — | Pulls `System.Drawing.Common`, which throws on non-Windows from .NET 6 on — treat as **Windows** |
| `PDF.Puppeteer` | PuppeteerSharp | ✓ A4 | — | — | **Downloads Chromium on first use** (`BrowserFetcher().DownloadAsync()`) — needs network + disk at runtime, or a pre-seeded cache |
| `PDF.MsPlaywright` | Microsoft.Playwright | ✓ A4 | — | — | **Installs its browser on first use** — same constraint; the install is guarded by a process-wide lock, so the first request pays for it |
| `PDF.DocNET` | Docnet.Core | — | merge, split, img↔pdf, text | — | Managed wrapper over a native library — the RID must be one `Docnet.Core` ships binaries for |
| `PDF.Spire` | FreeSpire.PDF | — | merge, split, img, text | ✓ | The **free** edition — the vendor caps document size/pages; confirm the current terms before relying on it |
| `PDF.PDFtoPrinter` | PDFtoPrinter | — | — | ✓ (Win) | Drives an external printing utility |
| `PDF.PockyBum522` | SimpleFreePdfPrinter | — | — | ✓ (Win) | Targets `net*-windows` — **will not build** on a non-Windows TFM |

**Recommendations:**
- HTML → PDF: **SelectPdf** on Windows (full options, nothing to download); **Puppeteer**/**Playwright**
  where the host is Linux or the CSS must be pixel-perfect and a first-run browser fetch is acceptable
- PDF operations: **DocNET** (merge, split, images, text extraction) — the only cross-platform ops backend
- Printing: **Spire** (operations + print) or **PDFtoPrinter** (print-only, Windows)

---

## Interfaces

### `IHtmlToPdfService`

```csharp
Task<IMemoryFile> Create(HtmlInput input, CancellationToken cancellationToken = default);
```

### `IPdfMerger`

```csharp
Task<IMemoryFile?>             Merge(IEnumerable<IMemoryFile> items, CancellationToken cancellationToken = default);
```

### `IPdfSplitter`

```csharp
Task<IEnumerable<IMemoryFile>>  Split(IMemoryFile pdf, IEnumerable<PdfSplitRange> ranges, CancellationToken cancellationToken = default);
Task<int>                       GetPageCount(IMemoryFile pdf, CancellationToken cancellationToken = default);
```

### `IPdfEditor` (extends `IPdfMerger` + `IPdfSplitter`)

```csharp
Task<IMemoryFile?>  RemovePages(IMemoryFile pdf, IEnumerable<int> pages, CancellationToken cancellationToken = default);
```

### `IPdfToImageService` / `IImagesToPdfService`

```csharp
Task<IList<IImageFile>>  ToImages(IMemoryFile pdf, PdfToImagesOptions? options = null, CancellationToken cancellationToken = default);
Task<IMemoryFile?>       ImagesToPdf(ImagesInput input, CancellationToken cancellationToken = default);
```

### `IPdfToImageAsyncService`

```csharp
IAsyncEnumerable<IImageFile>  ToImagesAsync(IMemoryFile pdf, PdfToImagesOptions? options = null);
```

### `IPdfTextExtractor`

```csharp
Task<string>          GetText(IMemoryFile pdf, CancellationToken cancellationToken = default);
```

### `IPdfTextService` (extends `IPdfTextExtractor`)

```csharp
Task<IList<string>>   GetTextPerPage(IMemoryFile pdf, CancellationToken cancellationToken = default);
Task<IMemoryFile?>    RemoveEmptyPages(IMemoryFile pdf, CancellationToken cancellationToken = default);
```

### `IPdfPrinter`

```csharp
string              DefaultPrinter { get; }
Task<IList<string>> List(CancellationToken cancellationToken = default);
Task                Print(PdfPrinterInput input, CancellationToken cancellationToken = default);
```

### `IPdfService`

Composite: `IPdfEditor + IPdfImageService + IPdfTextService`. Implemented by `PDF.DocNET.PdfManager` and `PDF.Spire.PdfManager`.

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

```csharp
// HTML → PDF (SelectPdf)
IHtmlToPdfService pdf = new Regira.Office.PDF.SelectPdf.PdfManager();
IMemoryFile file = await pdf.Create(new HtmlInput
{
    HtmlContent = "<h1>Invoice</h1>",
    Format      = PageSize.A4,
    Orientation = PageOrientation.Portrait
});

// Merge PDFs (DocNET)
IPdfMerger merger = new Regira.Office.PDF.DocNET.PdfManager(imageService);
IMemoryFile merged = (await merger.Merge([pdf1, pdf2, pdf3]))!;

// Extract text (DocNET)
IPdfTextExtractor extractor = new Regira.Office.PDF.DocNET.PdfManager(imageService);
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
