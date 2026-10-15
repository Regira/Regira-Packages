# Regira Office.OCR AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.OCR` | Optical character recognition via Tesseract and PaddleOCR |

Exact usings: `office.namespaces.md`.

**Related:**
- **IO.Storage** — `IMemoryFile` used as image input. `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`, or `io.storage.instructions.md` locally.
- **Media / Drawing** — image preprocessing before OCR. `get_package(id: "Regira.Media", section: "media.instructions")`, or `media.instructions.md` locally.

---

## Installation

```xml
<!-- Tesseract — configurable languages, cross-platform -->
<PackageReference Include="Regira.Office.OCR.Tesseract" Version="6.*" />

<!-- PaddleOCR — multilingual including Chinese, Windows-only -->
<PackageReference Include="Regira.Office.OCR.PaddleOCR" Version="6.*" />
```

---

## Backend Comparison

| Package | Engine | Languages | Platform |
|---|---|---|---|
| `Regira.Office.OCR.Tesseract` | Tesseract 5 | English, Dutch + configurable | Cross-platform |
| `Regira.Office.OCR.PaddleOCR` | PaddleOCR (ONNX) | 12 scripts: English, Latin, Chinese, Korean, Arabic, Cyrillic, East Slavic, Devanagari, Greek, Tamil, Telugu, Thai | Windows only |

---

## `IOcrService`

<!-- no-compile -->
```csharp
Task<OcrResult> Read(IMemoryFile imgFile, string? lang = null, CancellationToken cancellationToken = default);
```

`OcrResult` exposes `string Language` and `string? Text`.

Pass `lang` as an ISO 639-1 code (`"en"`, `"nl"`, `"zh"`). When `null`, Tesseract uses its `Options.Language` and
PaddleOCR the English model. When no text is detected, `Text` is an empty string, not `null`; it is declared `string?`,
so test it with `string.IsNullOrWhiteSpace`.

There is no `Add…` extension: register the backend yourself. Neither `OcrManager` keeps state between calls (each
`Read` builds its own engine), so a singleton is safe:

```csharp
using Regira.Office.OCR.Abstractions;

services.AddSingleton<IOcrService, Regira.Office.OCR.PaddleOCR.OcrManager>();
```

Tesseract takes its `Options` through the constructor, so register an instance:
`services.AddSingleton<IOcrService>(new Regira.Office.OCR.Tesseract.OcrManager(new() { DataDirectory = "./tessdata" }));`.

---

## Tesseract

Requires language data files (`tessdata` directory with `.traineddata` files).

<!-- no-compile -->
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

<!-- no-compile -->
```csharp
var ocr = new Regira.Office.OCR.PaddleOCR.OcrManager();

OcrResult result = await ocr.Read(imageFile);
string? text     = result.Text;

OcrResult zh = await ocr.Read(imageFile, lang: "zh");
OcrResult nl = await ocr.Read(imageFile, lang: "nl");
```

`lang` selects the PP-OCRv5 model for that language's **script**, not the language itself: `"zh"`/`"cn"` → Chinese,
`"ko"` → Korean, `"ar"`/`"fa"`/`"ur"`/`"ug"` → Arabic, `"ru"`/`"uk"`/`"be"` → East Slavic,
`"bg"`/`"mk"`/`"sr"`/`"mn"` → Cyrillic, `"hi"`/`"mr"`/`"ne"`/`"sa"` → Devanagari, `"el"` → Greek, `"ta"` → Tamil,
`"te"` → Telugu, `"th"` → Thai, and the Latin-script codes it lists (`"nl"`, `"fr"`, `"de"`, `"es"`, `"pl"`, `"tr"`,
`"vi"`, …) → Latin. `"en"`, unknown codes, and `null` use the English model.

> Depends on `OpenCvSharp4.runtime.win` — **Windows only**.

---

## Notes

- Input is any `IMemoryFile`: `bytes.ToMemoryFile("image/png")` or `ToBinaryFile()`.
- Tesseract is the cross-platform choice; PaddleOCR is Windows-only but bundles models automatically.

---
