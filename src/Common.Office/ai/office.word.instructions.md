# Regira Office.Word AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Word` | Word document creation, conversion, merge, and extraction |

**Related:**
- [Media / Drawing](../../Common.Media/ai/media.instructions.md) — `IImageFile` returned by `ToImages()`
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `IMemoryFile` used for document input/output

---

## Installation

```xml
<!-- Full-featured: create, convert, merge, extract (recommended) -->
<PackageReference Include="Regira.Office.Word.Spire" Version="6.*" />

<!-- Lightweight create-only -->
<PackageReference Include="Regira.Office.Word.Mini" Version="6.*" />
```

---

## Backend Comparison

| Package | Backend | Create | Convert | Merge | Extract |
|---|---|---|---|---|---|
| `Word.Spire` | FreeSpire.Doc | ✓ | ✓ | ✓ | ✓ |
| `Word.Mini` | MiniWord | ✓ | — | — | — |

**Recommendation:** Use **Word.Spire** for all operations. Use **Word.Mini** only when a lightweight create-only solution is required (net10.0+).

> **FreeSpire.Doc limit:** Up to 500 paragraphs or 25 tables per document.

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

Composite of all the above. `Word.Spire.WordManager` implements this. `IWordManager` is an obsolete alias (`[Obsolete]`, inherits `IWordService`) — resolve `IWordService`.

---

## Models

### `WordTemplateInput`

| Property | Type | Description |
|---|---|---|
| `Template` | `IMemoryFile` | Source `.docx` template |
| `GlobalParameters` | `IDictionary<string, object>?` | Simple `{{Key}}` replacements |
| `CollectionParameters` | `IDictionary<string, ICollection<IDictionary<string, object>>>?` | Table row data — key matches a table placeholder |
| `Images` | `ICollection<WordImage>?` | Image replacements (matched by name) |
| `DocumentParameters` | `IDictionary<string, WordTemplateInput>?` | Insert nested documents at bookmarks |
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
| `Settings` | `DocumentSettings?` | `null` | Override page size / orientation / margins |

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
| `Type` | `HeaderFooterType` | `Default`, `FirstPage`, `Even`, `Odd` |

---

## Usage

```csharp
IWordService word = new Regira.Office.Word.Spire.WordManager();

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

### HTML Parameters (Word.Spire only)

Prefix `GlobalParameters` keys with `html_` to inject raw HTML:

```csharp
GlobalParameters = new Dictionary<string, object>
{
    ["html_Notes"] = "<p>This is <strong>bold</strong> text.</p>"
}
```

---
