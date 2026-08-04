# Regira Office.OCR AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.OCR` | Optical character recognition via Tesseract and PaddleOCR |

**Related:**
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `IMemoryFile` used as image input
- [Media / Drawing](../../Common.Media/ai/media.instructions.md) — image preprocessing before OCR

---

## Installation

```xml
<!-- Tesseract — configurable languages, cross-platform -->
<PackageReference Include="Regira.Office.OCR.Tesseract" Version="6.*" />

<!-- PaddleOCR — multilingual including Chinese, Windows-only -->
<PackageReference Include="Regira.Office.OCR.PaddleOCR" Version="6.*" />
```

> Add the Regira feed to `NuGet.Config`:
> ```xml
> <add key="Regira" value="https://packages.regira.com/v3/index.json" />
> ```

---

## Backend Comparison

| Package | Engine | Languages | Platform |
|---|---|---|---|
| `Regira.Office.OCR.Tesseract` | Tesseract 5 | English, Dutch + configurable | Cross-platform |
| `Regira.Office.OCR.PaddleOCR` | PaddleOCR (ONNX) | English, Chinese | Windows only |

---

## `IOcrService`

```csharp
Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken cancellationToken = default);
```

`OcrResult` exposes `string Language` and `string? Text`.

Pass `lang` as an ISO 639-1 code (`"en"`, `"nl"`, `"zh"`). When `null`, the implementation's configured default is used.
Both implementations set `OcrResult.Text` to `null` when no text is detected.

---

## Tesseract

Requires language data files (`tessdata` directory with `.traineddata` files).

```csharp
var ocr = new Regira.Office.OCR.Tesseract.OcrManager(new OcrManager.Options
{
    Language      = "en",
    DataDirectory = "./tessdata"
});

OcrResult result = await ocr.Read(imageFile);
string? text     = result.Text;

OcrResult nl = await ocr.Read(imageFile, lang: "nl");
```

Download language packs from `github.com/tesseract-ocr/tessdata`.

---

## PaddleOCR

Uses bundled local model files (`Sdcb.PaddleOCR.Models.LocalV5`). No external data directory required.

```csharp
var ocr = new Regira.Office.OCR.PaddleOCR.OcrManager();

OcrResult result = await ocr.Read(imageFile);
string? text     = result.Text;

OcrResult zh = await ocr.Read(imageFile, lang: "zh");
OcrResult nl = await ocr.Read(imageFile, lang: "nl");
```

`lang` selects the PP-OCRv5 model for that language's **script**, not the language itself: `"zh"`/`"cn"` → Chinese,
`"ko"` → Korean, `"ru"`/`"uk"`/`"be"` → East Slavic, `"el"` → Greek, `"th"` → Thai, other Latin-script codes
(`"nl"`, `"fr"`, `"de"`, `"es"`, …) → Latin. `"en"`, unknown codes, and `null` use the English model.

> Depends on `OpenCvSharp4.runtime.win` — **Windows only**.

---

## Notes

- Input is `IMemoryFile` — use `bytes.ToBinaryFile()` or any `IFileService.GetBytes()` result.
- Tesseract is the cross-platform choice; PaddleOCR is Windows-only but bundles models automatically.

---
