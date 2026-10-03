# Regira.Office.PDF.DocNET

PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [Docnet.Core](https://www.nuget.org/packages/Docnet.Core). `PdfManager` implements `IPdfService`: merge, split, page count and page removal, text extraction, and conversion between images and PDF. It is the recommended backend for PDF operations and, unlike PDF.Spire, covers the whole of `IPdfService`.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.DocNET" Version="6.*" />
```

The constructor takes an `IImageService` from [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/) (Regira.Drawing.SkiaSharp, for example) for the image conversions.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — the shared interfaces and models, how the backends compare, and DocNET's implementation notes
- [PDF examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/examples.html) — merge, split and text extraction with DocNET

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
