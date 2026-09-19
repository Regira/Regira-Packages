# Regira Office.Word AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Word` | Word document creation, conversion, merge, and extraction |

**Related:**
- **Media / Drawing** — `IImageFile` returned by `ToImages()`. `get_package(id: "Regira.Media", section: "media.instructions")`, or `media.instructions.md` locally.
- **IO.Storage** — `IMemoryFile` used for document input/output. `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`, or `io.storage.instructions.md` locally.

---

## Installation

```xml
<!-- Full-featured: create, convert, merge, extract (recommended) -->
<PackageReference Include="Regira.Office.Word.Spire" Version="6.*" />

<!-- Full-featured, commercial licence -->
<PackageReference Include="Regira.Office.Word.Syncfusion" Version="6.*" />

<!-- Full-featured, loads ODT and writes EPUB, commercial licence -->
<PackageReference Include="Regira.Office.Word.Aspose" Version="6.*" />

<!-- Lightweight: create and extract, no vendor licence -->
<PackageReference Include="Regira.Office.Word.Mini" Version="6.*" />

<!-- PDF conversion and page images through a Gotenberg server, no vendor licence -->
<PackageReference Include="Regira.Office.Word.Gotenberg" Version="6.*" />
```

---

## Backend Comparison

| Package | Backend | Create | Convert | Merge | Extract | Licence / limits |
|---|---|---|---|---|---|---|
| `Word.Spire` | FreeSpire.Doc | ✓ | ✓ | ✓ | ✓ | Free edition: 500 paragraphs or 25 tables per document |
| `Word.Syncfusion` | Syncfusion DocIO | ✓ | ✓ except EPub | ✓ | ✓ | Commercial licence key required; no size cap |
| `Word.Aspose` | Aspose.Words | ✓ | ✓ | ✓ | ✓ | Commercial licence required; unlicensed output is watermarked and truncated |
| `Word.Mini` | MiniWord | partial | — | — | text, images | MIT, no key, no size cap |
| `Word.Gotenberg` | Gotenberg server (LibreOffice) | — | PDF only | — | page images | MIT; needs a running Gotenberg server |

**Recommendation:** Use **Word.Spire** by default — the widest format coverage, and no vendor key. Use **Word.Syncfusion** when documents exceed the FreeSpire size cap and a Syncfusion licence is already in place. Use **Word.Aspose** when an Aspose licence is in place, or when ODT templates must load and EPUB must be written — it is the only backend that does both. Use **Word.Mini** when conversion and merging are not needed, and pair it with **Word.Gotenberg** for PDF output and page images without a vendor licence — as long as the templates need only what Word.Mini renders (see its limits).

> **FreeSpire.Doc limit:** Up to 500 paragraphs or 25 tables per document.

> **Word.Syncfusion limits:** ODT templates cannot be **loaded** (saving to ODT works), and EPUB export is unavailable on .NET Core. `Convert` throws `NotSupportedException` for both, and for `Png`/`Jpeg` (use `ToImages`). Without a valid licence key DocIO prepends *"Created with a trial version of Syncfusion Word library or registered the wrong key in your application"* to every document it produces — including conversions and rendered pages. It is ordinary body text, so a containment check on your own content still passes; assert the banner is **absent** if you need to know the key works. Document SDK is priced per developer per year with a minimum team size ([syncfusion.com/sales/products](https://www.syncfusion.com/sales/products)); their Community Licence page names Document Solution SDKs among the products it covers, but the Document Solutions pages do not corroborate that and mention only a 30-day evaluation — confirm eligibility with Syncfusion before relying on a free tier. PDF conversion and `ToImages` render through SkiaSharp 3.119.1 and HarfBuzzSharp 8.3.1.2; on Linux, add `SkiaSharp.NativeAssets.Linux` and `HarfBuzzSharp.NativeAssets.Linux` at the versions of `SkiaSharp` and `HarfBuzzSharp` the application resolves (those two, unless another package raises them).

> **Word.Mini limits:** `Create` renders `GlobalParameters`, `CollectionParameters` and `Images`, with MiniWord's template syntax rather than the other backends': every value fills a `{{tag}}` — a collection fills a table row whose cells hold `{{Items.Name}}` tags (the row repeats per item, and `{{ row_number }}` is not provided), and an image replaces a `{{logo}}` tag, not a picture named by its Alt Text. A template written for Word.Spire therefore renders its global parameters identically and its collection tables and images not at all. Spaces inside the braces are ignored, and a tag Word split over several runs still matches. It throws `NotSupportedException` for `DocumentParameters`, `Headers`, `Footers` and any non-default `InputOptions` — MiniWord has no API for them. A key may appear in only one of `GlobalParameters`, `Images` and `CollectionParameters`; they share one `{{tag}}` namespace and a duplicate throws `ArgumentException`. `GetText` and `GetImages` run against the rendered document, and `ToImages` is unavailable (no layout engine).

> **Word.Aspose limits:** ODT templates load and EPUB is written; `Convert` writes every document format and throws `NotSupportedException` only for `Png`/`Jpeg` (use `ToImages`). Without a licence Aspose.Words runs in evaluation mode: every document gets *"Created with an evaluation copy of Aspose.Words…"* at the top and *"Evaluation Only. Created with Aspose.Words…"* in place of its own headers and footers, and documents beyond a few hundred paragraphs are cut short. So constructing `WordService` throws when no licence is configured anywhere — a configuration key that resolves to nothing fails the first time the service is built — unless `AsposeWordConfig.AllowEvaluation` accepts evaluation output, or the process already holds a licence. Both banners are ordinary text, so a containment check on your own content still passes — assert they are **absent**, and check the last paragraph of a long document survives, if you need to know the licence works. Aspose sells developer, site and metered licences, which differ in the number of developers and locations and in whether public-facing web apps and SaaS are covered ([purchase.aspose.com/pricing/words/net](https://purchase.aspose.com/pricing/words/net/)); a free 30-day temporary licence is available on request. On Linux, add `SkiaSharp.NativeAssets.Linux` at the version of `SkiaSharp` the application resolves (3.119 or later) and install `libfontconfig1` and `libharfbuzz-icu0`.

> **Word.Gotenberg limits:** Implements `IWordConverter` and `IWordToImagesService` only — Gotenberg has no document model. `Convert` produces PDF only; every other `FileFormat` throws `NotSupportedException`. It reads Word (`.doc`, `.dot`, `.docx`, `.dotx`, `.docm`, `.dotm`), OpenDocument (`.odt`, `.ott`), `.rtf`, `.txt`, `.html`/`.htm` and `.epub` sources. `ConversionOptions.Settings` needs an OOXML source (`.docx`, `.dotx`, `.docm`, `.dotm`), because the page size, orientation and margins are written into the document before upload; any `PageSize` is honoured. An input carrying template substitutions needs an `IWordCreator` that renders it first, and throws `NotSupportedException` without one: `Word.Mini.WordService` covers `GlobalParameters`, `CollectionParameters` and `Images` (in its own template syntax), while `Headers`, `Footers`, `DocumentParameters` and non-default `InputOptions` need a creator with a document model — Word.Spire, Word.Syncfusion or Word.Aspose — because Word.Mini refuses them. `ToImages` needs an `IPdfToImageService` such as `Regira.Office.PDF.DocNET`, which rasterises the PDF. **LibreOffice lays documents out differently from Word:** a font missing from the Gotenberg image is substituted, which moves line and page breaks, so page images and page counts can differ from Word's — install the fonts your documents use in the image. The server enforces its own time limit (`--api-timeout`, 30 seconds by default) and answers 503 when a conversion exceeds it.

---

## Interfaces

### `IWordCreator`

```csharp
Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default);
```

### `IWordConverter`

```csharp
Task<IMemoryFile> Convert(WordTemplateInput input, FileFormat format, CancellationToken cancellationToken = default);
Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default);
```

### `IWordMerger`

```csharp
Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default);
```

### `IWordTextExtractor` / `IWordImageExtractor` / `IWordToImagesService`

```csharp
Task<string>                    GetText(WordTemplateInput input, CancellationToken cancellationToken = default);
Task<IEnumerable<WordImage>>    GetImages(WordTemplateInput input, CancellationToken cancellationToken = default);
Task<IEnumerable<IImageFile>>   ToImages(WordTemplateInput input, CancellationToken cancellationToken = default);    // one image per page
```

### `IWordService`

Composite of all the above. `Word.Spire.WordService`, `Word.Syncfusion.WordService` and `Word.Aspose.WordService` implement this; `Word.Mini.WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` only, and `Word.Gotenberg.WordService` implements `IWordConverter` and `IWordToImagesService` only — resolve the narrowest interface you need. `IWordManager` is an obsolete alias (`[Obsolete]`, inherits `IWordService`) — resolve `IWordService`.

---

## Models

### `WordTemplateInput`

| Property | Type | Description |
|---|---|---|
| `Template` | `IMemoryFile` | Source `.docx` template |
| `GlobalParameters` | `IDictionary<string, object>?` | Simple `{{Key}}` replacements |
| `CollectionParameters` | `IDictionary<string, ICollection<IDictionary<string, object>>>?` | Table row data — key matches a table placeholder |
| `Images` | `ICollection<WordImage>?` | Image replacements (matched by name) |
| `DocumentParameters` | `IDictionary<string, WordTemplateInput>?` | Nested documents, each inserted in place of a `<{ key }>` placeholder paragraph |
| `Headers` | `ICollection<WordHeaderFooterInput>?` | Page headers |
| `Footers` | `ICollection<WordHeaderFooterInput>?` | Page footers |
| `Options` | `InputOptions?` | Processing behaviour flags |

### `InputOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `InheritFont` | `bool` | `false` | Apply template's Normal style font to inserted content |
| `HorizontalAlignment` | `HorizontalAlignment?` | `null` | Force text alignment |
| `RemoveEmptyParagraphs` | `bool` | `false` | Strip blank paragraphs after substitution |
| `EnforceEvenAmountOfPages` | `bool` | `false` | Insert page break if page count is odd |

### `ConversionOptions`

| Property | Type | Default | Description |
|---|---|---|---|
| `OutputFormat` | `FileFormat` | `Docx` | Target format |
| `AutoScaleTables` | `bool` | `true` | Resize tables to fit new page width |
| `AutoScalePictures` | `bool` | `true` | Resize images to fit new page width |
| `Settings` | `DocumentSettings?` | `null` | Override page size / orientation / margins — of **every** section: a document mixing portrait and landscape sections comes out in one orientation |

### `DocumentSettings`

| Property | Type | Default | Description |
|---|---|---|---|
| `PageSize` | `PageSize` | `A4` | Paper format |
| `PageOrientation` | `PageOrientation` | `Portrait` | Orientation |
| `Margins` | `Margins?` | `null` | Override margins (in points) |

### `FileFormat`

```
Docx  Doc  Dotx  Dot  Docm  Dotm  Pdf  Html  Rtf  Odt  EPub  Jpeg  Png
```

### `WordImage`

| Property | Type | Description |
|---|---|---|
| `Name` | `string` | Matches image placeholder name in the template |
| `File` | `IMemoryFile` | Image bytes |
| `Size` | `ImageSize?` | Override image dimensions |

### `WordHeaderFooterInput`

| Property | Type | Description |
|---|---|---|
| `Template` | `IMemoryFile` | Template fragment for the header/footer |
| `Type` | `HeaderFooterType` | `Default`, `FirstPage`, `Even`, `Odd` — `FirstPage` and `Even` give those pages stories of their own, headers and footers alike: the `Default` story then serves the other pages, and a story given no `FirstPage`/`Even` version repeats its default on those pages |

---

## Usage

```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();

// Create from template
IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template         = templateBytes.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object>
    {
        ["CustomerName"] = "Alice",
        ["InvoiceDate"]  = DateTime.Today.ToString("d")
    }
});

// Convert to PDF
IMemoryFile pdf = await word.Convert(new WordTemplateInput { Template = doc }, FileFormat.Pdf);

// Extract text
string text = await word.GetText(new WordTemplateInput { Template = doc });
```

### HTML Parameters (Word.Spire, Word.Syncfusion and Word.Aspose)

Prefix `GlobalParameters` keys with `html_` to inject raw HTML:

```csharp
GlobalParameters = new Dictionary<string, object>
{
    ["html_Notes"] = "<p>This is <strong>bold</strong> text.</p>"
}
```

---

## Registration

The backends are constructed directly; register one under the interfaces the application injects. Word.Syncfusion
and Word.Aspose take their licence through the constructor, because their vendor needs it before the first document
is touched:

```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();

builder.Services.AddSingleton(new SyncfusionWordConfig { LicenseKey = builder.Configuration["Syncfusion:LicenseKey"] });
builder.Services.AddTransient<IWordService, Regira.Office.Word.Syncfusion.WordService>();

builder.Services.AddSingleton(new AsposeWordConfig { LicensePath = builder.Configuration["Aspose:LicensePath"] });
// or, from a secret store: LicenseBase64 = builder.Configuration["Aspose:License"]
builder.Services.AddTransient<IWordConverter, Regira.Office.Word.Aspose.WordService>();
```

Both licences also resolve from environment variables — `SYNCFUSION_LICENSE_KEY`, and
`ASPOSE_WORDS_LICENSE` (the licence file, Base64-encoded) or `ASPOSE_WORDS_LICENSE_PATH` — so a host that
already sets them needs no configuration. Each is applied once per process. When none of Aspose's four
settings resolves, constructing the service throws; set `AllowEvaluation = true` to accept evaluation output
instead (a trial, a test run).

`Word.Gotenberg` has a DI extension because it talks to a server through a typed `HttpClient`. It
registers `IWordConverter` and `IWordToImagesService`, and takes an `IPdfToImageService` (for
`ToImages`) and an `IWordCreator` (for template substitutions) from the container when they are there:

```csharp
services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();
services.AddSingleton<IPdfToImageService, Regira.Office.PDF.DocNET.PdfManager>();
services.AddSingleton<IWordCreator, Regira.Office.Word.Mini.WordService>();
services.AddGotenbergWord(o =>
{
    o.BaseUrl = builder.Configuration["Gotenberg:BaseUrl"]!;   // e.g. http://gotenberg:3000
    o.Timeout = TimeSpan.FromMinutes(2);                       // raise --api-timeout on the server too
});
```

Its `HttpClient` is named `Regira.Office.Word.Gotenberg.DependencyInjection.ServiceCollectionExtensions.HttpClientName`
(add handlers or resilience through `services.AddHttpClient(HttpClientName)`), so it keeps its own base address and
credentials beside `AddOfficeClients`. Both register an `IWordConverter`, and the one registered **last** is
the one resolved — call `AddGotenbergWord` after `AddOfficeClients` to convert with Gotenberg while the Regira
Office API serves the rest, its `IPdfToImageService` included. For template input that needs headers, footers
or nested documents, register a backend with a document model as the `IWordCreator` —
`builder.Services.AddTransient<IWordCreator, Regira.Office.Word.Aspose.WordService>()` beside its config.
