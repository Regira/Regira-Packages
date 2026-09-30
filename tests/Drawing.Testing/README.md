# Drawing.Testing

NUnit tests for the two `IImageService` backends of [Regira Drawing](../../src/Common.Media/README.md):
[Drawing.GDI](../../src/Drawing.GDI/README.md) and [Drawing.SkiaSharp](../../src/Drawing.SkiaSharp/README.md). Each
backend runs the same suite — format conversion, resize, crop, rotate, transparency, drawing positions and text, and
`ImageBuilder` composition — alongside color and dimension tests.

## Running

```bash
dotnet test tests/Drawing.Testing
```

Needs Windows x64: the GDI fixtures use System.Drawing (GDI+), and the text tests read the drawn text back with
[OCR.PaddleOCR](../../src/OCR.PaddleOCR/README.md), whose native runtime packages are Windows x64 only. There is no
platform guard, so elsewhere those tests fail rather than skip.

Source images are in `Assets/Input`. The tests generate blank and single-color inputs into `Assets/Input/GDI` and
`Assets/Input/SkiaSharp`, and write their results to `Assets/Output/GDI` and `Assets/Output/SkiaSharp`; all four
folders are git-ignored.
