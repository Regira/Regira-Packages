# Regira.Office.OCR.PaddleOCR

OCR backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [Sdcb.PaddleOCR](https://www.nuget.org/packages/Sdcb.PaddleOCR). `OcrManager` implements `IOcrService`; the `lang` code picks the PP-OCRv5 model for that language's script — Latin, Chinese, Cyrillic, Arabic, Devanagari and others — with English as the fallback. Unlike the Tesseract backend it needs no language data files: the models ship with the package.

## Installation

```xml
<PackageReference Include="Regira.Office.OCR.PaddleOCR" Version="6.*" />
```

Windows only — it depends on the Windows native runtimes of OpenCvSharp and Paddle Inference (`OpenCvSharp4.runtime.win`, `Sdcb.PaddleInference.runtime.win64.mkl`).

## Documentation

- [OCR](https://regira.github.io/Regira-Packages/src/Common.Office/docs/ocr/) — the `IOcrService` contract, how the Tesseract and PaddleOCR backends compare, and which model each `lang` code selects

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
