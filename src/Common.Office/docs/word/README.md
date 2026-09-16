# Regira Office.Word

Regira Office.Word provides Word document creation from templates, conversion, merging, and content extraction.

## Projects

| Project | Package | Backend | Create | Convert | Merge | Extract |
|---------|---------|---------|--------|---------|-------|---------|
| `Common.Office` | *(transitive)* | Shared abstractions | — | — | — | — |
| `Word.Spire` | `Regira.Office.Word.Spire` | FreeSpire.Doc | ✓ | ✓ | ✓ | ✓ |
| `Word.Syncfusion` | `Regira.Office.Word.Syncfusion` | Syncfusion DocIO | ✓ | ✓ except EPUB | ✓ | ✓ |
| `Word.Mini` | `Regira.Office.Word.Mini` | MiniWord | partial | — | — | text, images |

## Installation

```xml
<!-- Full-featured (recommended) -->
<PackageReference Include="Regira.Office.Word.Spire" Version="6.*" />

<!-- Full-featured, commercial licence -->
<PackageReference Include="Regira.Office.Word.Syncfusion" Version="6.*" />

<!-- Lightweight: create and extract, no vendor licence -->
<PackageReference Include="Regira.Office.Word.Mini" Version="6.*" />
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

Composite of all the above. `Word.Spire.WordService` and `Word.Syncfusion.WordService` implement this. `Word.Mini.WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` only, so depend on the narrowest interface you need. An `[Obsolete]` alias `IWordManager : IWordService` remains for backward compatibility.

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
| `Settings` | `DocumentSettings?` | `null` | Override page size / orientation / margins |

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
| `Type` | `HeaderFooterType` | `Default`, `FirstPage`, `Even`, `Odd` |

## Implementation notes

### Word.Spire (recommended)

`WordService` implements `IWordService` — the full capability set. Supports HTML parameters (`html_*` prefix in `GlobalParameters` injects raw HTML). Converts to PDF, HTML, RTF, ODT, EPUB, and image formats. Handles nested document insertion via `DocumentParameters`.

> **Limit:** FreeSpire.Doc free edition supports documents up to 500 paragraphs or 25 tables.

### Word.Syncfusion

`WordService` implements `IWordService` — the full capability set, on Syncfusion DocIO. No document size cap, and the same template features as Word.Spire (`html_*` parameters, collection tables by Alt-Text Title, nested documents, headers and footers). Unlike Word.Spire, `Convert` reports the content type of the format it actually produced.

Register it, so the licence key reaches DocIO before the first document:

```csharp
services.AddSyncfusionWord(o => o.LicenseKey = configuration["Syncfusion:LicenseKey"]);
```

The key also resolves from the `SYNCFUSION_LICENSE_KEY` environment variable.

> **Licence:** required. Without a valid key DocIO prepends *"Created with a trial version of Syncfusion Word library or registered the wrong key in your application"* to every document, conversion and rendered page. It is ordinary body text rather than a hard failure, so check for its absence to confirm a key actually works. Syncfusion lists Document Solutions at $1,199 per developer per year with a five-developer minimum. Their Community Licence page names Document Solution SDKs among the products it covers, but the Document Solutions sales and licensing pages do not corroborate that and mention only a 30-day evaluation, so confirm eligibility with Syncfusion rather than assuming a free tier.

> **Format limits:** ODT templates cannot be loaded (saving to ODT works) and EPUB export is unavailable on .NET Core. Both throw `NotSupportedException`, as do `Png`/`Jpeg` output (use `ToImages`).

> **Rendering:** PDF conversion and `ToImages` need `Syncfusion.DocIORenderer`, which is SkiaSharp-based. On Linux, add `SkiaSharp.NativeAssets.Linux` and `HarfBuzzSharp.NativeAssets.Linux`.

### Word.Mini

`WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` (`WordCreator` remains as an `[Obsolete]` alias). Lightweight and MIT-licensed, with no document size cap. Text and image extraction read the rendered document through the Open XML SDK, which MiniWord already depends on.

`Convert`, `Merge` and `ToImages` are unavailable: MiniWord has no layout engine. Use Word.Spire for those.

`Create` honours `GlobalParameters`, `CollectionParameters` and `Images`, and throws `NotSupportedException` for `DocumentParameters`, `Headers`, `Footers` and any non-default `InputOptions`.

> **Shared template namespace:** `GlobalParameters`, `Images` and `CollectionParameters` all resolve against the same `{{tag}}` placeholders, so a key may appear in only one of them — a duplicate throws `ArgumentException`.

## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — Template substitution, conversion, merge, and extraction
