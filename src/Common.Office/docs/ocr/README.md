# Regira OCR

Regira OCR provides optical character recognition via two underlying engines. Both implement the same `IOcrService` interface.

## Projects

| Project | Package | Engine | Languages |
|---------|---------|--------|-----------|
| `OCR.Tesseract` | `Regira.Office.OCR.Tesseract` | Tesseract 5 | English bundled; others from their `.traineddata` |
| `OCR.PaddleOCR` | `Regira.Office.OCR.PaddleOCR` | PaddleOCR (ONNX) | Multi-script — 12 PP-OCRv5 models (see table below) |

## Installation

```xml
<!-- Tesseract (configurable languages) -->
<PackageReference Include="Regira.Office.OCR.Tesseract" Version="6.*" />

<!-- PaddleOCR (multilingual, including Chinese) -->
<PackageReference Include="Regira.Office.OCR.PaddleOCR" Version="6.*" />
```

## IOcrService

<!-- no-compile -->
```csharp
Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken cancellationToken = default);
```

Pass `lang` as an ISO 639-1 code (e.g. `"en"`, `"nl"`, `"zh"`). When `null`, Tesseract falls back to its configured `Options.Language`; PaddleOCR has no configurable default and uses the English model.

### OcrResult

| Property | Type | Description |
|----------|------|-------------|
| `Language` | `string` | Language used for recognition |
| `Text` | `string?` | Recognized text — declared nullable, but both engines return an empty string when no text is detected |

## Tesseract

Reads its language data from `DataDirectory`, `./tessdata` by default. The build copies the bundled `eng.traineddata` to the output's `tessdata/`, and a relative `DataDirectory` that does not exist under the working directory resolves against the output folder (`AppContext.BaseDirectory`), so the default works under `dotnet run` too. An empty `DataDirectory` leaves the folder to Tesseract's `TESSDATA_PREFIX`, for a system `tessdata`.

```csharp
var ocr = new Regira.Office.OCR.Tesseract.OcrManager(new()
{
    Language      = "en",              // default language
    DataDirectory = "./tessdata"       // path to .traineddata files
});

IMemoryFile imageFile = File.ReadAllBytes("scan.png").ToMemoryFile();
OcrResult result = await ocr.Read(imageFile);
string? text     = result.Text;

OcrResult nl = await ocr.Read(imageFile, lang: "nl");
```

Download language packs from `github.com/tesseract-ocr/tessdata`.

## PaddleOCR

Uses local model files (bundled via `Sdcb.PaddleOCR.Models.LocalV5`). No external data directory required.

```csharp
var ocr = new Regira.Office.OCR.PaddleOCR.OcrManager();

IMemoryFile imageFile = File.ReadAllBytes("scan.png").ToMemoryFile();
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

- Input is `IMemoryFile` — use `bytes.ToBinaryFile()`; an `IFileService.GetBytes()` result is `byte[]?`, so convert it with `.ToBinaryFile()` too.
- Both implementations set `OcrResult.Text` to an empty string when no text is detected — never `null`.
- Tesseract bundles its Windows natives (x86, x64); on Linux or macOS, install the Tesseract 5 and Leptonica natives. PaddleOCR is Windows-only.
