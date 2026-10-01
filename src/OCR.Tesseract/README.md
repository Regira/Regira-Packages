# Regira.Office.OCR.Tesseract

OCR backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on the [Tesseract](https://www.nuget.org/packages/Tesseract) wrapper for Tesseract 5. `OcrManager` implements `IOcrService` and reads the text in an image, in the language its `Options` name (`en` by default) or the one a call passes.

## Installation

```xml
<PackageReference Include="Regira.Office.OCR.Tesseract" Version="6.*" />
```

- The English trained data ships in the package, and its build targets copy it into the build and publish output as `tessdata/eng.traineddata`, the default `Options.DataDirectory`. For another language, add its `.traineddata` file from [tessdata](https://github.com/tesseract-ocr/tessdata) or the smaller [tessdata_fast](https://github.com/tesseract-ocr/tessdata_fast) to that folder, or point `DataDirectory` at a folder of your own. Set the MSBuild property `RegiraTesseractCopyTessData` to `false` to leave the English data out.
- The Tesseract package brings native binaries for Windows x86 and x64, which need the [Visual C++ redistributable](https://learn.microsoft.com/cpp/windows/latest-supported-vc-redist).

## Documentation

- [OCR](https://regira.github.io/Regira-Packages/src/Common.Office/docs/ocr/) — the `IOcrService` contract, the result model, and how the Tesseract and PaddleOCR backends compare

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
