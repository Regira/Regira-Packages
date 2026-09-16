# Regira Office.Word

Regira Office.Word provides Word document creation from templates, conversion, merging, and content extraction.

## Projects

| Project | Package | Backend | Create | Convert | Merge | Extract |
|---------|---------|---------|--------|---------|-------|---------|
| `Common.Office` | *(transitive)* | Shared abstractions | — | — | — | — |
| `Word.Spire` | `Regira.Office.Word.Spire` | FreeSpire.Doc | ✓ | ✓ | ✓ | ✓ |
| `Word.Syncfusion` | `Regira.Office.Word.Syncfusion` | Syncfusion DocIO | ✓ | ✓ except EPUB | ✓ | ✓ |
| `Word.Aspose` | `Regira.Office.Word.Aspose` | Aspose.Words | ✓ | ✓ | ✓ | ✓ |
| `Word.Mini` | `Regira.Office.Word.Mini` | MiniWord | partial | — | — | text, images |
| `Word.Gotenberg` | `Regira.Office.Word.Gotenberg` | Gotenberg server (LibreOffice) | — | PDF only | — | page images |

## Installation

```xml
<!-- Full-featured (recommended) -->
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

## Quick Start

```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();

byte[] templateBytes = await File.ReadAllBytesAsync("template.docx");
IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template         = templateBytes.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object>
    {
        ["CustomerName"] = "Alice",
        ["InvoiceDate"]  = DateTime.Today.ToString("d")
    }
});
```

## Interfaces

### IWordCreator

```csharp no-compile
Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default);
```

### IWordConverter

```csharp no-compile
Task<IMemoryFile> Convert(WordTemplateInput input, FileFormat format, CancellationToken cancellationToken = default);
Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default);
```

### IWordMerger

```csharp no-compile
Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default);
```

### IWordTextExtractor / IWordImageExtractor / IWordToImagesService

```csharp no-compile
Task<string>                    GetText(WordTemplateInput input, CancellationToken cancellationToken = default);
Task<IEnumerable<WordImage>>    GetImages(WordTemplateInput input, CancellationToken cancellationToken = default);
Task<IEnumerable<IImageFile>>   ToImages(WordTemplateInput input, CancellationToken cancellationToken = default);  // one image per page
```

### IWordService

Composite of all the above. `Word.Spire.WordService`, `Word.Syncfusion.WordService` and `Word.Aspose.WordService` implement this. `Word.Mini.WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` only, and `Word.Gotenberg.WordService` implements `IWordConverter` and `IWordToImagesService` only, so depend on the narrowest interface you need. An `[Obsolete]` alias `IWordManager : IWordService` remains for backward compatibility.

## WordTemplateInput

| Property | Type | Description |
|----------|------|-------------|
| `Template` | `IMemoryFile` | Source .docx template |
| `GlobalParameters` | `IDictionary<string, object>?` | Simple `{{Key}}` replacements |
| `CollectionParameters` | `IDictionary<string, ICollection<IDictionary<string, object>>>?` | Table row data — key matches a table placeholder in the template |
| `Images` | `ICollection<WordImage>?` | Image replacements (matched by name) |
| `DocumentParameters` | `IDictionary<string, WordTemplateInput>?` | Insert nested documents at bookmarks |
| `Headers` | `ICollection<WordHeaderFooterInput>?` | Page headers |
| `Footers` | `ICollection<WordHeaderFooterInput>?` | Page footers |
| `Options` | `InputOptions?` | Processing behaviour flags |

### InputOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `InheritFont` | `bool` | `false` | Apply template's Normal style font to inserted content |
| `HorizontalAlignment` | `HorizontalAlignment?` | `null` | Force text alignment |
| `RemoveEmptyParagraphs` | `bool` | `false` | Strip blank paragraphs after substitution |
| `EnforceEvenAmountOfPages` | `bool` | `false` | Insert page break if page count is odd |

### ConversionOptions

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `OutputFormat` | `FileFormat` | `Docx` | Target format |
| `AutoScaleTables` | `bool` | `true` | Resize tables to fit new page width |
| `AutoScalePictures` | `bool` | `true` | Resize images to fit new page width |
| `Settings` | `DocumentSettings?` | `null` | Override page size / orientation / margins of every section — a document mixing portrait and landscape sections comes out in one orientation |

### DocumentSettings

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `PageSize` | `PageSize` | `A4` | Paper format |
| `PageOrientation` | `PageOrientation` | `Portrait` | Orientation |
| `Margins` | `Margins?` | `null` | Override margins (in points) |

### FileFormat

```
Docx  Doc  Dotx  Dot  Docm  Dotm  Pdf  Html  Rtf  Odt  EPub  Jpeg  Png
```

### WordImage

| Property | Type | Description |
|----------|------|-------------|
| `Name` | `string` | Matches image placeholder name in the template |
| `File` | `IMemoryFile?` | Image bytes |
| `Size` | `ImageSize?` | Override image dimensions |
| `HorizontalAlignment` | `HorizontalAlignment?` | Optional image alignment |

### WordHeaderFooterInput

| Property | Type | Description |
|----------|------|-------------|
| `Template` | `IMemoryFile` | Template fragment for the header/footer |
| `Type` | `HeaderFooterType` | `Default`, `FirstPage`, `Even`, `Odd` — `Even` also switches the document to separate odd and even pages, so the `Default` header then serves odd pages only |

## Implementation notes

### Word.Spire (recommended)

`WordService` implements `IWordService` — the full capability set. Supports HTML parameters (`html_*` prefix in `GlobalParameters` injects raw HTML). Converts to PDF, HTML, RTF, ODT, EPUB, and image formats. Handles nested document insertion via `DocumentParameters`.

> **Limit:** FreeSpire.Doc free edition supports documents up to 500 paragraphs or 25 tables.

### Word.Syncfusion

`WordService` implements `IWordService` — the full capability set, on Syncfusion DocIO. No document size cap, and the same template features as Word.Spire (`html_*` parameters, collection tables by Alt-Text Title, nested documents, headers and footers). Unlike Word.Spire, `Convert` reports the content type of the format it actually produced.

Register it, so the licence key reaches DocIO before the first document:

```csharp
using Regira.Office.Word.Syncfusion.DependencyInjection;

IServiceCollection services  = new ServiceCollection();
IConfiguration configuration = new ConfigurationBuilder().Build();

services.AddSyncfusionWord(o => o.LicenseKey = configuration["Syncfusion:LicenseKey"]);
```

The key also resolves from the `SYNCFUSION_LICENSE_KEY` environment variable.

> **Licence:** required. Without a valid key DocIO prepends *"Created with a trial version of Syncfusion Word library or registered the wrong key in your application"* to every document, conversion and rendered page. It is ordinary body text rather than a hard failure, so check for its absence to confirm a key actually works. Document SDK is priced per developer per year with a minimum team size ([Syncfusion pricing](https://www.syncfusion.com/sales/products)). Their Community Licence page names Document Solution SDKs among the products it covers, but the Document Solutions sales and licensing pages do not corroborate that and mention only a 30-day evaluation, so confirm eligibility with Syncfusion rather than assuming a free tier.

> **Format limits:** ODT templates cannot be loaded (saving to ODT works) and EPUB export is unavailable on .NET Core. Both throw `NotSupportedException`, as do `Png`/`Jpeg` output (use `ToImages`).

> **Rendering:** PDF conversion and `ToImages` need `Syncfusion.DocIORenderer`, which renders through SkiaSharp 3.119.1 and HarfBuzzSharp 8.3.1.2. On Linux, add `SkiaSharp.NativeAssets.Linux` and `HarfBuzzSharp.NativeAssets.Linux` at the versions of `SkiaSharp` and `HarfBuzzSharp` the application resolves — those two, unless another package raises them.

### Word.Aspose

`WordService` implements `IWordService` — the full capability set, on Aspose.Words, with the same template features as Word.Spire (`html_*` parameters, collection tables by Alt-Text Title, nested documents, headers and footers). It is the one backend that both loads ODT templates and writes EPUB, and `Convert` writes every document format. `Convert` reports the content type of the format it actually produced, and page settings honour every `PageSize`, A0 to A10.

Register it, so the licence reaches Aspose.Words before the first document:

```csharp
using Regira.Office.Word.Aspose.DependencyInjection;

IServiceCollection services  = new ServiceCollection();
IConfiguration configuration = new ConfigurationBuilder().Build();

services.AddAsposeWord(o => o.LicensePath = configuration["Aspose:LicensePath"]);
```

`LicenseBase64` takes the licence file's content instead, for a host that keeps it in a secret store. Without either, the `ASPOSE_WORDS_LICENSE` (Base64 content) and `ASPOSE_WORDS_LICENSE_PATH` environment variables are used. The licence is applied once per process.

When none of the four resolves — a configuration key that is missing, say — constructing `WordService` throws rather than produce evaluation output unnoticed. Set `AllowEvaluation` to accept that output, for a trial or a test run; a process that already holds a licence needs neither.

> **Licence:** required. Without one Aspose.Words runs in evaluation mode: it puts *"Created with an evaluation copy of Aspose.Words…"* at the top of every document and *"Evaluation Only. Created with Aspose.Words…"* in its header — conversions and rendered pages included — and cuts documents short after a few hundred paragraphs. The banners are ordinary text, so check for their absence, and that the end of a long document survives, to confirm a licence works. Aspose sells developer, site and metered licences, which differ in the number of developers and locations and in whether public-facing web apps and SaaS are covered ([Aspose.Words for .NET pricing](https://purchase.aspose.com/pricing/words/net/)). A free 30-day temporary licence is available on request.

> **Format limits:** `Convert` throws `NotSupportedException` only for `Png`/`Jpeg` output (use `ToImages`).

> **Rendering:** PDF conversion and `ToImages` render through SkiaSharp (3.119 or later). On Linux, add `SkiaSharp.NativeAssets.Linux` at the version of `SkiaSharp` the application resolves, and install `libfontconfig1` and `libharfbuzz-icu0`.

### Word.Mini

`WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` (`WordCreator` remains as an `[Obsolete]` alias). Lightweight and MIT-licensed, with no document size cap. Text and image extraction read the rendered document through the Open XML SDK, which MiniWord already depends on.

`Convert`, `Merge` and `ToImages` are unavailable: MiniWord has no layout engine. Use Word.Spire for those, or pair Word.Mini with Word.Gotenberg for PDF output and page images.

`Create` renders `GlobalParameters`, `CollectionParameters` and `Images`, and throws `NotSupportedException` for `DocumentParameters`, `Headers`, `Footers` and any non-default `InputOptions`.

> **Template syntax:** MiniWord's, not Word.Spire's. Every value fills a `{{tag}}`: a collection fills a table row whose cells hold `{{Items.Name}}` tags, and the row repeats per item (there is no `{{ row_number }}`); an image replaces a `{{logo}}` tag rather than a picture named by its Alt Text. A template written for the other backends renders its global parameters the same way and leaves its collection tables and pictures as they are. Spaces inside the braces are ignored, and a tag Word split over several runs while it was edited still matches.

> **Shared template namespace:** `GlobalParameters`, `Images` and `CollectionParameters` all resolve against the same `{{tag}}` placeholders, so a key may appear in only one of them — a duplicate throws `ArgumentException`.

### Word.Gotenberg

`WordService` implements `IWordConverter` and `IWordToImagesService`, through the LibreOffice route of a [Gotenberg](https://gotenberg.dev) server (MIT, a Docker image bundling LibreOffice and Chromium). It is the route to PDF output and page images without a vendor licence.

- **PDF only.** `Convert` produces PDF; every other `FileFormat` throws `NotSupportedException`. Sources can be Word (`.doc`, `.dot`, `.docx`, `.dotx`, `.docm`, `.dotm`), OpenDocument (`.odt`, `.ott`), `.rtf`, `.txt`, `.html`/`.htm` or `.epub` — the format is read from the file name when the template is a named file, otherwise from its content.
- **Templates.** Gotenberg converts finished documents. An input carrying `GlobalParameters`, `CollectionParameters`, `Images`, `DocumentParameters`, `Headers`, `Footers` or non-default `InputOptions` is rendered first by the `IWordCreator` the service was given, and throws `NotSupportedException` without one. Word.Mini renders the first three, in its own template syntax; headers, footers, nested documents and input options need a creator with a document model — Word.Spire, Word.Syncfusion or Word.Aspose — since Word.Mini refuses them.
- **Page settings.** The LibreOffice route has no page-size or margin fields, so `ConversionOptions.Settings` is written into the document's section properties before upload, together with the table and picture scaling. That needs an OOXML source (`.docx`, `.dotx`, `.docm`, `.dotm`), and honours every `PageSize`.
- **Page images.** Gotenberg has no route that rasterises a PDF. `ToImages` converts to PDF and hands the result to the `IPdfToImageService` the service was given — `Regira.Office.PDF.DocNET`, for example — which returns one image per page.
- **Layout.** LibreOffice lays a document out differently from Word. A font missing from the Gotenberg image is substituted, which moves line and page breaks, so page images and page counts can differ from what Word shows. Install the fonts your documents use in the image.

Start a server and register the service:

```bash
docker run --rm -p 3000:3000 gotenberg/gotenberg:8
```

```csharp
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.Word.Gotenberg.DependencyInjection;

IServiceCollection services  = new ServiceCollection();
IConfiguration configuration = new ConfigurationBuilder().Build();

// optional collaborators, taken from the container when present
services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();
services.AddSingleton<IPdfToImageService, Regira.Office.PDF.DocNET.PdfManager>();  // for ToImages
services.AddSingleton<IWordCreator, Regira.Office.Word.Mini.WordService>();         // for template input

services.AddGotenbergWord(o =>
{
    o.BaseUrl = configuration["Gotenberg:BaseUrl"] ?? "http://localhost:3000";
    o.Timeout = TimeSpan.FromMinutes(2);
});
```

`AddGotenbergWord` registers `IWordConverter` and `IWordToImagesService` on one `HttpClient`, named `ServiceCollectionExtensions.HttpClientName` — add handlers or resilience through `services.AddHttpClient(ServiceCollectionExtensions.HttpClientName)`. `Username` and `Password` send basic authentication, for a server started with `--api-enable-basic-auth`; `ImageOptions` sets the size and format of the page images. Without DI, construct `WordService` with an `HttpClient` whose `BaseAddress` points at the server.

> **Beside `AddOfficeClients`:** the two keep separate clients, so neither's base address or credentials reach the other's server. Both register an `IWordConverter`, and the one registered last is resolved: call `AddGotenbergWord` after `AddOfficeClients` to convert with Gotenberg while the Regira Office API serves the rest — its `IPdfToImageService` included.

> **Timeouts:** the server enforces its own limit per request (`--api-timeout`, 30 seconds by default) and answers `503` when a conversion exceeds it; the exception message says so. Raise it on the server as well as `Timeout` here for large documents.

## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — Template substitution, conversion, merge, and extraction
