# Regira OCR

Regira OCR provides optical character recognition via two underlying engines. Both implement the same `IOcrService` interface.

## Projects

| Project | Package | Engine | Languages |
|---------|---------|--------|-----------|
| `OCR.Tesseract` | `Regira.Office.OCR.Tesseract` | Tesseract 5 | English, Dutch (configurable) |
| `OCR.PaddleOCR` | `Regira.Office.OCR.PaddleOCR` | PaddleOCR (ONNX) | English, Chinese |

## Installation

```xml
<!-- Tesseract (configurable languages) -->
<PackageReference Include="Regira.Office.OCR.Tesseract" Version="6.*" />

<!-- PaddleOCR (multilingual, including Chinese) -->
<PackageReference Include="Regira.Office.OCR.PaddleOCR" Version="6.*" />
```

## IOcrService

```csharp
Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken cancellationToken = default);
```

Pass `lang` as an ISO 639-1 code (e.g. `"en"`, `"nl"`, `"zh"`). When `null`, the implementation's configured default is used.

### OcrResult

| Property | Type | Description |
|----------|------|-------------|
| `Language` | `string` | Language used for recognition |
| `Text` | `string?` | Recognized text — `null` when no text is detected |

## Tesseract

Requires language data files in the `tessdata` directory.

```csharp
var ocr = new Regira.Office.OCR.Tesseract.OcrManager(new OcrManager.Options
{
    Language      = "en",              // default language
    DataDirectory = "./tessdata"       // path to .traineddata files
});

OcrResult result = await ocr.Read(imageFile);
string? text     = result.Text;

OcrResult nl = await ocr.Read(imageFile, lang: "nl");
```

Download language packs from `github.com/tesseract-ocr/tessdata`.

## PaddleOCR

Uses local model files (bundled via `Sdcb.PaddleOCR.Models.LocalV5`). No external data directory required.

```csharp
var ocr = new Regira.Office.OCR.PaddleOCR.OcrManager();

OcrResult result = await ocr.Read(imageFile);
string? text     = result.Text;

OcrResult zh = await ocr.Read(imageFile, lang: "zh");
OcrResult nl = await ocr.Read(imageFile, lang: "nl");
```

`lang` selects the PP-OCRv5 model for that language's **script**, not the language itself:

| `lang` | Model |
| --- | --- |
| `"en"`, unknown, `null` | English |
| `"nl"`, `"fr"`, `"de"`, `"es"`, `"it"`, `"pt"`, … | Latin |
| `"zh"`, `"cn"` | Chinese |
| `"ko"` | Korean |
| `"ru"`, `"uk"`, `"be"` | East Slavic |
| `"bg"`, `"mk"`, `"sr"`, `"mn"` | Cyrillic |
| `"hi"`, `"mr"`, `"ne"`, `"sa"` | Devanagari |
| `"ar"`, `"fa"`, `"ur"`, `"ug"` | Arabic |
| `"el"` / `"ta"` / `"te"` / `"th"` | Greek / Tamil / Telugu / Thai |

> PaddleOCR depends on **OpenCvSharp4.runtime.win** — Windows only.

## Notes

- Input is `IMemoryFile` — use `bytes.ToBinaryFile()` or any `IFileService.GetBytes()` result.
- Both implementations set `OcrResult.Text` to `null` when no text is detected.
- Tesseract is cross-platform; PaddleOCR is Windows-only.
