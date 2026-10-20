# Regira.Office.PDF.PdfPig

PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [PdfPig](https://www.nuget.org/packages/PdfPig) and [PDFtoImage](https://www.nuget.org/packages/PDFtoImage). `PdfService` implements `IPdfService`: merge, split, page count and page removal, text extraction, and conversion between images and PDF. It is the recommended backend for PDF operations.

PdfPig is fully managed and does the merging, splitting, text extraction and images to PDF. PDFtoImage renders page images through PDFium, which ships native binaries for Windows, Linux and macOS (x64 and arm64).

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.PdfPig" Version="6.*" />
```

On Linux there is nothing to add: the package carries SkiaSharp's dependency-free native library. An application that adds `SkiaSharp.NativeAssets.Linux` itself, as [Regira.Drawing.SkiaSharp](https://regira.github.io/Regira-Packages/src/Drawing.SkiaSharp/) describes, gets that library instead, which needs `libfontconfig1` on the host.

The constructor takes an `IImageService` from [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/) (Regira.Drawing.SkiaSharp, for example), which reads the images going into a PDF and produces the page images coming out of one.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — the shared interfaces and models, how the backends compare, and PdfPig's implementation notes: page layout of images to PDF, page image sizes, and which pages count as empty
- [PDF examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/examples.html) — merge, split, page images and text extraction

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). PdfPig is Apache-2.0 licensed, PDFtoImage MIT, and the PDFium binaries it ships (bblanchon.PDFium) Apache-2.0. A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
