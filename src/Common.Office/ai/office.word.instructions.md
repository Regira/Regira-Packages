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

<!-- Full-featured, commercial licence -->
<PackageReference Include="Regira.Office.Word.Syncfusion" Version="6.*" />

<!-- Lightweight: create and extract, no vendor licence -->
<PackageReference Include="Regira.Office.Word.Mini" Version="6.*" />
```

---

## Backend Comparison

| Package | Backend | Create | Convert | Merge | Extract | Licence / limits |
|---|---|---|---|---|---|---|
| `Word.Spire` | FreeSpire.Doc | ✓ | ✓ | ✓ | ✓ | Free edition: 500 paragraphs or 25 tables per document |
| `Word.Syncfusion` | Syncfusion DocIO | ✓ | ✓ except EPub | ✓ | ✓ | Commercial licence key required; no size cap |
| `Word.Mini` | MiniWord | partial | — | — | text, images | MIT, no key, no size cap |

**Recommendation:** Use **Word.Spire** by default — the widest format coverage, and no vendor key. Use **Word.Syncfusion** when documents exceed the FreeSpire size cap and a Syncfusion licence is already in place. Use **Word.Mini** when conversion and merging are not needed.

> **FreeSpire.Doc limit:** Up to 500 paragraphs or 25 tables per document.

> **Word.Syncfusion limits:** ODT templates cannot be **loaded** (saving to ODT works), and EPUB export is unavailable on .NET Core. `Convert` throws `NotSupportedException` for both, and for `Png`/`Jpeg` (use `ToImages`). Without a valid licence key DocIO prepends *"Created with a trial version of Syncfusion Word library or registered the wrong key in your application"* to every document it produces — including conversions and rendered pages. It is ordinary body text, so a containment check on your own content still passes; assert the banner is **absent** if you need to know the key works. Syncfusion prices Document Solutions at $1,199 per developer per year with a five-developer minimum; their Community Licence page names Document Solution SDKs among the products it covers, but the Document Solutions pages do not corroborate that and mention only a 30-day evaluation — confirm eligibility with Syncfusion before relying on a free tier.

> **Word.Mini limits:** `Create` honours `GlobalParameters`, `CollectionParameters` and `Images`. It throws `NotSupportedException` for `DocumentParameters`, `Headers`, `Footers` and any non-default `InputOptions` — MiniWord has no API for them. A key may appear in only one of `GlobalParameters`, `Images` and `CollectionParameters`; they share one `{{tag}}` namespace and a duplicate throws `ArgumentException`. `GetText` and `GetImages` run against the rendered document, and `ToImages` is unavailable (no layout engine).

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

Composite of all the above. `Word.Spire.WordService` and `Word.Syncfusion.WordService` implement this; `Word.Mini.WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` only — resolve the narrowest interface you need. `IWordManager` is an obsolete alias (`[Obsolete]`, inherits `IWordService`) — resolve `IWordService`.

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

### HTML Parameters (Word.Spire and Word.Syncfusion)

Prefix `GlobalParameters` keys with `html_` to inject raw HTML:

```csharp
GlobalParameters = new Dictionary<string, object>
{
    ["html_Notes"] = "<p>This is <strong>bold</strong> text.</p>"
}
```

---

## Registration

`Word.Syncfusion` is the only Word backend with a DI extension, because DocIO needs its licence key
before the first document is touched:

```csharp
services.AddSyncfusionWord(o => o.LicenseKey = builder.Configuration["Syncfusion:LicenseKey"]);
```

The key also resolves from the `SYNCFUSION_LICENSE_KEY` environment variable, so a host that already
sets it needs no configuration. It is registered once per process.

The other backends are constructed directly:

```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();
```
