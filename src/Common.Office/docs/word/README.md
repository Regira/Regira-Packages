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

<!-- no-compile -->
```csharp
Task<IMemoryFile> Create(WordTemplateInput input, CancellationToken cancellationToken = default);
```

### IWordConverter

<!-- no-compile -->
```csharp
Task<IMemoryFile> Convert(WordTemplateInput input, FileFormat format, CancellationToken cancellationToken = default);
Task<IMemoryFile> Convert(WordTemplateInput input, ConversionOptions options, CancellationToken cancellationToken = default);
```

The returned file's `ContentType` names the format produced — `application/pdf` for `FileFormat.Pdf`, `text/html` for `Html` — so a web action can serve it with that type.

### IWordMerger

<!-- no-compile -->
```csharp
Task<IMemoryFile> Merge(IEnumerable<WordTemplateInput> inputs, CancellationToken cancellationToken = default);
```

### IWordTextExtractor / IWordImageExtractor / IWordToImagesService

<!-- no-compile -->
```csharp
Task<string>                    GetText(WordTemplateInput input, CancellationToken cancellationToken = default);
Task<IEnumerable<WordImage>>    GetImages(WordTemplateInput input, CancellationToken cancellationToken = default);
Task<IEnumerable<IImageFile>>   ToImages(WordTemplateInput input, CancellationToken cancellationToken = default);  // one image per page
```

### IWordService

Composite of all the above. `Word.Spire.WordService`, `Word.Syncfusion.WordService` and `Word.Aspose.WordService` implement this. `Word.Mini.WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` only, and `Word.Gotenberg.WordService` implements `IWordConverter` and `IWordToImagesService` only, so depend on the narrowest interface you need. `IWordManager : IWordService` is an `[Obsolete]` alias — depend on `IWordService`.

## WordTemplateInput

| Property | Type | Description |
|----------|------|-------------|
| `Template` | `IMemoryFile` | Source .docx template |
| `GlobalParameters` | `IDictionary<string, object>?` | Simple `{{Key}}` replacements; also decide `{{#if Key}}` [conditional blocks](#conditional-blocks) |
| `CollectionParameters` | `IDictionary<string, ICollection<IDictionary<string, object>>>?` | Table rows — the key is the Alt Text title of the [table they fill](#collection-tables); `{{#if Key}}` holds when it has rows |
| `Images` | `ICollection<WordImage>?` | Image replacements (matched by name) |
| `DocumentParameters` | `IDictionary<string, WordTemplateInput>?` | Nested documents, each inserted in place of a `<{ key }>` placeholder paragraph |
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
| `Type` | `HeaderFooterType` | `Default`, `FirstPage`, `Even`, `Odd` — `FirstPage` and `Even` give those pages stories of their own, headers and footers alike: the `Default` story then serves the other pages, and a story given no `FirstPage`/`Even` version repeats its default on those pages |

## Collection tables

On Word.Spire, Word.Syncfusion and Word.Aspose, a `CollectionParameters` entry fills the table whose Alt Text title
(Table Properties → Alt Text → Title) is its key. The table's second row is the template row: it is written once per
row of the collection, in order, and the rows around it stay where they are — a header above it, a totals row below it:

```text
Alt Text title: Lines
| Nr               | Description       | Price         |
| {{ row_number }} | {{ Description }} | € {{ Price }} |
| Total            |                   | {{ Total }}   |
```

```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();

byte[] templateBytes = await File.ReadAllBytesAsync("order.docx");
var orderLines = new[] { (Description: "Consulting", Price: 1000m), (Description: "Travel", Price: 250m) };

IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template             = templateBytes.ToMemoryFile(),
    GlobalParameters     = new Dictionary<string, object> { ["Total"] = orderLines.Sum(l => l.Price).ToString("N2") },
    CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
    {
        ["Lines"] = orderLines.Select(l => (IDictionary<string, object>)new Dictionary<string, object>
        {
            ["Description"] = l.Description,
            ["Price"]       = l.Price.ToString("N2")
        }).ToList()
    }
});
```

- The first row stays as it is — a header, which may be left empty — so the template row is always the second.
  Rows below it are ordinary content: `{{ Total }}` above is filled from `GlobalParameters`.
- `{{ row_number }}` is the row's position, from 1. Every other tag in the template row reads the collection row,
  regardless of case, and a tag the row does not have is left empty — a `GlobalParameters` key included, so keep
  global values out of the template row. A tag whose key holds anything but ASCII letters, digits, `_` and `.` is
  not a row field and is left for `GlobalParameters`.
- Only the first paragraph of each cell is filled.
- Values are written with `ToString()`: format numbers, amounts and dates in code.
- The title is matched exactly, case included, and one table is filled per title — give each table its own. A key
  without a table is skipped, and an empty collection leaves the table without its template row: wrap the table in a
  [conditional block](#conditional-blocks) to drop it together with its heading.
- Word.Mini fills collections in [MiniWord's own syntax](#wordmini) instead; Word.Gotenberg fills them through its
  `IWordCreator`.

## Conditional blocks

Word.Spire, Word.Syncfusion, Word.Aspose and Word.Mini keep or drop a stretch of a template depending on a
parameter. Each marker stands alone in its own paragraph:

```text
{{#if IsPaid}}
Thank you for your payment of {{Amount}}.
{{else}}
Please pay {{Amount}} before {{DueDate}}.
{{/if}}
```

```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();

byte[] templateBytes = await File.ReadAllBytesAsync("invoice.docx");
DateTime dueDate = DateTime.Today.AddDays(30);
decimal amountPaid = 0m;

IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template         = templateBytes.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object>
    {
        ["IsPaid"]  = amountPaid > 0,
        ["Amount"]  = "€ 1 250,00",
        ["DueDate"] = dueDate.ToString("d")
    }
});
```

- `{{#if Key}}` holds when `GlobalParameters[Key]` is set to anything but `null`, `false`, an empty or blank
  string, zero or an empty collection. The texts `"false"` and `"0"` hold: convert a form or query-string value to
  `bool` before passing it. A JSON value is read by its kind, so a JSON `false`, `0`, `[]` or `{}` is false. An enum
  value always holds, its zero member included: compare it in code (`["IsDraft"] = status == Status.Draft`). A
  `CollectionParameters` key holds when its collection has rows, so `{{#if Items}}` drops a heading together with
  its empty table. A key found in neither is false, and keys match regardless of case, an exact match first.
- `{{#if !Key}}` negates. `{{else}}` is optional, and blocks nest.
- Markers are read from the text Word shows: one in a field code or a tracked deletion is not a marker, and a
  marker edited under track changes reads as edited.
- The branch that does not hold goes with everything in it — paragraphs, tables, a `<{ key }>` placeholder — and
  the marker paragraphs go too, whichever branch holds, with anything else they carry: a page break, a bookmark, a
  picture anchored to one. Keep those in a paragraph of their own; only a section break stored on a marker
  paragraph stays. Blocks are resolved before anything is filled, so a dropped branch's parameters, images and
  nested documents are never processed.
- There is no comparison syntax: compute the flag in code (`["IsOverdue"] = dueDate < DateTime.Today`).
- A block inside a collection table's template row is decided once, for every row.
- A block opens and closes in the same body, table cell, text box, content control, header or footer, and within
  one section — a content control around whole paragraphs is a container of its own, so a block cannot open outside
  one and close inside it. Footnotes, endnotes and comments are not read for markers.
- A document uses blocks when one of its paragraphs is `{{#if Key}}` or `{{#if !Key}}` and nothing else, and only
  then are its blocks resolved. Everything else in a document that uses none stays as it is — marker text among
  other text, a stray `{{else}}` or `{{/if}}` — so a finished document that writes about templates converts and
  reads unchanged.
- The document `Create` fills is read as a template by any later call. A parameter value that fills a paragraph
  with nothing but a marker — `{{#if X}}` typed into a form — makes that output use blocks: a later `GetText`,
  `Convert` or `ToImages` of it throws, or drops everything between two such values. Keep user-entered values out
  of paragraphs of their own, or produce the final format in the call that fills the template, rather than
  converting the stored output later.
- In a document that uses blocks, these throw `FormatException`: a block that does not open and close in one
  container and section, a `{{/if}}` or `{{else}}` without its `{{#if}}`, a second `{{else}}`, a marker sharing its
  paragraph with other text, and a marker this syntax does not know, such as `{{#unless X}}` or `{{else if X}}`.

## Implementation notes

### Word.Spire (recommended)

`WordService` implements `IWordService` — the full capability set. Supports HTML parameters (`html_*` prefix in `GlobalParameters` injects raw HTML). Converts to PDF, HTML, RTF, ODT and EPUB, and renders pages as images with `ToImages`. Handles nested document insertion via `DocumentParameters`.

> **Limits:** the FreeSpire.Doc free edition supports documents up to 500 paragraphs or 25 tables, and writes at most three pages of PDF. Converting a longer document to PDF gives its first three pages followed by a notice page — *"Spire Doc. Free version converting word documents to PDF files, you can only get the first 3 page of PDF file."* — and raises no error. `ToImages` and the other formats (HTML, RTF, ODT, EPUB) carry every page. For longer PDFs, use Word.Syncfusion or Word.Aspose (licensed), or Word.Gotenberg (no vendor licence).

### Word.Syncfusion

`WordService` implements `IWordService` — the full capability set, on Syncfusion DocIO. No document size cap, and the same template features as Word.Spire (`html_*` parameters, collection tables by Alt-Text Title, nested documents, headers and footers).

The licence key goes to the constructor, which hands it to DocIO before the first document. In a container, register the configuration and the service under the interfaces the application injects:

```csharp
using Regira.Office.Word.Syncfusion;

IServiceCollection services  = new ServiceCollection();
IConfiguration configuration = new ConfigurationBuilder().Build();

services.AddSingleton(new SyncfusionWordConfig { LicenseKey = configuration["Syncfusion:LicenseKey"] });
services.AddTransient<IWordService, WordService>();

// or without a container
var word = new WordService(new SyncfusionWordConfig { LicenseKey = configuration["Syncfusion:LicenseKey"] });
```

The key also resolves from the `SYNCFUSION_LICENSE_KEY` environment variable, and is registered once per process.

> **Licence:** required. Without a valid key DocIO prepends *"Created with a trial version of Syncfusion Word library or registered the wrong key in your application"* to every document, conversion and rendered page. It is ordinary body text rather than a hard failure, so check for its absence to confirm a key actually works. Document SDK is priced per developer per year with a minimum team size ([Syncfusion pricing](https://www.syncfusion.com/sales/products)). Their Community Licence page names Document Solution SDKs among the products it covers, but the Document Solutions sales and licensing pages do not corroborate that and mention only a 30-day evaluation, so confirm eligibility with Syncfusion rather than assuming a free tier.

> **Format limits:** ODT templates cannot be loaded (saving to ODT works) and EPUB export is unavailable on .NET Core. Both throw `NotSupportedException`, as do `Png`/`Jpeg` output (use `ToImages`).

> **Rendering:** PDF conversion and `ToImages` need `Syncfusion.DocIORenderer`, which renders through SkiaSharp 4.150.1 and HarfBuzzSharp 14.2.1.1. On Linux, add `SkiaSharp.NativeAssets.Linux` and `HarfBuzzSharp.NativeAssets.Linux` at the versions of `SkiaSharp` and `HarfBuzzSharp` the application resolves — those two, unless another package raises them.

### Word.Aspose

`WordService` implements `IWordService` — the full capability set, on Aspose.Words, with the same template features as Word.Spire (`html_*` parameters, collection tables by Alt-Text Title, nested documents, headers and footers). It is the one backend that both loads ODT templates and writes EPUB, and `Convert` writes every document format. Page settings honour every `PageSize`, A0 to A10.

The licence goes to the constructor, which applies it before the first document. In a container, register the configuration and the service under the interfaces the application injects:

```csharp
using Regira.Office.Word.Aspose;

IServiceCollection services  = new ServiceCollection();
IConfiguration configuration = new ConfigurationBuilder().Build();

services.AddSingleton(new AsposeWordConfig { LicensePath = configuration["Aspose:LicensePath"] });
services.AddTransient<IWordConverter, WordService>();
services.AddTransient<IWordTextExtractor, WordService>();
```

`LicenseBase64` takes the licence file's content instead, for a host that keeps it in a secret store. Without either, the `ASPOSE_WORDS_LICENSE` (Base64 content) and `ASPOSE_WORDS_LICENSE_PATH` environment variables are used. The licence is applied once per process.

When none of the four resolves — a configuration key that is missing, say — constructing `WordService` throws rather than produce evaluation output unnoticed. Set `AllowEvaluation` to accept that output, for a trial or a test run; a process that already holds a licence needs neither.

> **Licence:** required. Without one Aspose.Words runs in evaluation mode: it puts *"Created with an evaluation copy of Aspose.Words…"* at the top of every document and *"Evaluation Only. Created with Aspose.Words…"* in place of its own headers and footers — conversions and rendered pages included — and cuts documents short after a few hundred paragraphs. The banners are ordinary text, so check for their absence, and that the end of a long document survives, to confirm a licence works. Aspose sells developer, site and metered licences, which differ in the number of developers and locations and in whether public-facing web apps and SaaS are covered ([Aspose.Words for .NET pricing](https://purchase.aspose.com/pricing/words/net/)). A free 30-day temporary licence is available on request.

> **Format limits:** `Convert` throws `NotSupportedException` only for `Png`/`Jpeg` output (use `ToImages`).

> **Rendering:** PDF conversion and `ToImages` render through SkiaSharp (3.119 or later). On Linux, add `SkiaSharp.NativeAssets.Linux` at the version of `SkiaSharp` the application resolves, and install `libfontconfig1` and `libharfbuzz-icu0`.

### Word.Mini

`WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` (`WordCreator` is an `[Obsolete]` alias). Lightweight and Apache-2.0-licensed, with no document size cap. Text and image extraction read the rendered document through the Open XML SDK, which MiniWord already depends on.

`Convert`, `Merge` and `ToImages` are unavailable: MiniWord has no layout engine. Use Word.Spire for those, or pair Word.Mini with Word.Gotenberg for PDF output and page images.

`Create` renders `GlobalParameters`, `CollectionParameters` and `Images`, and throws `NotSupportedException` for `DocumentParameters`, `Headers`, `Footers` and any non-default `InputOptions`.

> **Template syntax:** MiniWord's, not Word.Spire's. Every value fills a `{{tag}}`: a collection fills a table row whose cells hold `{{Items.Name}}` tags, and the row repeats per item (there is no `{{ row_number }}`); an image replaces a `{{logo}}` tag rather than a picture named by its Alt Text. A template written for the other backends renders its global parameters the same way and leaves its collection tables and pictures as they are. Spaces inside the braces are ignored, and a tag Word split over several runs while it was edited still matches. [Conditional blocks](#conditional-blocks) work as on the other backends, because Word.Mini resolves them before MiniWord renders; use them rather than MiniWord's own `@if` paragraphs, which compare `true`/`false` as text — `@if Flag == true` never holds — and throw on a bare `@if Flag`.

> **Shared template namespace:** `GlobalParameters`, `Images` and `CollectionParameters` all resolve against the same `{{tag}}` placeholders, so a key may appear in only one of them — a duplicate throws `ArgumentException`.

### Word.Gotenberg

`WordService` implements `IWordConverter` and `IWordToImagesService`, through the LibreOffice route of a [Gotenberg](https://gotenberg.dev) server (MIT, a Docker image bundling LibreOffice and Chromium). It is the route to PDF output and page images without a vendor licence.

- **PDF only.** `Convert` produces PDF; every other `FileFormat` throws `NotSupportedException`. Sources can be Word (`.doc`, `.dot`, `.docx`, `.dotx`, `.docm`, `.dotm`), OpenDocument (`.odt`, `.ott`), `.rtf`, `.txt`, `.html`/`.htm` or `.epub` — the format is read from the file name when the template is a named file, otherwise from its content.
- **Templates.** Gotenberg converts finished documents. An input carrying `GlobalParameters`, `CollectionParameters`, `Images`, `DocumentParameters`, `Headers`, `Footers` or non-default `InputOptions` is rendered first by the `IWordCreator` the service was given, and throws `NotSupportedException` without one. So is an OOXML template holding a [conditional block](#conditional-blocks), even without parameters, since a key the input does not give is false. Word.Mini renders the first three, in its own template syntax; headers, footers, nested documents and input options need a creator with a document model — Word.Spire, Word.Syncfusion or Word.Aspose — since Word.Mini refuses them.
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
