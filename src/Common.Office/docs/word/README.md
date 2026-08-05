# Regira Office.Word

Regira Office.Word provides Word document creation from templates, conversion, merging, and content extraction.

## Projects

| Project | Package | Backend | Create | Convert | Merge | Extract |
|---------|---------|---------|--------|---------|-------|---------|
| `Common.Office` | *(transitive)* | Shared abstractions | — | — | — | — |
| `Word.Spire` | `Regira.Office.Word.Spire` | FreeSpire.Doc | ✓ | ✓ | ✓ | ✓ |
| `Word.Mini` | `Regira.Office.Word.Mini` | MiniWord | ✓ | — | — | — |

## Installation

```xml
<!-- Full-featured (recommended) -->
<PackageReference Include="Regira.Office.Word.Spire" Version="6.*" />

<!-- Lightweight create-only -->
<PackageReference Include="Regira.Office.Word.Mini" Version="6.*" />
```

## Quick Start

```csharp
IWordService word = new Regira.Office.Word.Spire.WordManager();

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

Composite of all the above. `Word.Spire.WordManager` implements this. An `[Obsolete]` alias `IWordManager : IWordService` remains for backward compatibility.

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

`WordManager` implements `IWordService` — the full capability set. Supports HTML parameters (`html_*` prefix in `GlobalParameters` injects raw HTML). Converts to PDF, HTML, RTF, ODT, EPUB, and image formats. Handles nested document insertion via `DocumentParameters`.

> **Limit:** FreeSpire.Doc free edition supports documents up to 500 paragraphs or 25 tables.

### Word.Mini

`WordCreator` implements only `IWordCreator`. Lightweight — uses MiniWord, no conversion or extraction. Works only on net10.0.

## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — Template substitution, conversion, merge, and extraction
