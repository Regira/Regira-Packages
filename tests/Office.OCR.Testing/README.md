# Office.OCR.Testing

Tests for [OCR.Tesseract](../../src/OCR.Tesseract/README.md) and [OCR.PaddleOCR](../../src/OCR.PaddleOCR/README.md):
reading an English and a Dutch sample image (`Assets/poem-en.jpg`, `Assets/poem-nl.jpg`), and PaddleOCR's
language-to-model mapping. NUnit.

## Running

```bash
dotnet test tests/Office.OCR.Testing
```

Needs Windows: Tesseract loads its bundled x86/x64 Windows binaries, and PaddleOCR uses the Windows runtime packages
(`Sdcb.PaddleInference.runtime.win64.mkl`, `OpenCvSharp4.runtime.win`). There is no platform guard, so on other
platforms the tests fail rather than skip. The Tesseract models (`eng`, `nld`) are in `tessdata/` and copied to the
output; the PaddleOCR models come from NuGet packages. No network or secrets.
