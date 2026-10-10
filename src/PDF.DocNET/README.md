# Regira.Office.PDF.DocNET

PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [Docnet.Core](https://www.nuget.org/packages/Docnet.Core). `PdfManager` implements `IPdfService`: merge, split, page count and page removal, text extraction, and conversion between images and PDF.

This package is deprecated: new applications use [Regira.Office.PDF.PdfPig](https://regira.github.io/Regira-Packages/src/PDF.PdfPig/), which implements the same `IPdfService`. Docnet.Core has had no release since 2.6.0 (2023) and bundles a PDFium build from 2022; PDFium parses every PDF it is given, so an outdated build is a risk for uploaded files.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.DocNET" Version="6.*" />
```

The constructor takes an `IImageService` from [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/) (Regira.Drawing.SkiaSharp, for example) for the image conversions.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — the shared interfaces and models, how the backends compare, and DocNET's implementation notes

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
