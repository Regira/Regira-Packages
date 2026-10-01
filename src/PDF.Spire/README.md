# Regira.Office.PDF.Spire

PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [FreeSpire.PDF](https://www.nuget.org/packages/FreeSpire.PDF). `PdfManager` implements `IPdfMerger`, `IPdfSplitter`, `IPdfToImageService` and `IPdfTextExtractor` — a subset of `IPdfService`, with image conversion in the PDF→image direction only — and `PdfPrinter` implements `IPdfPrinter` for printing.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.Spire" Version="6.*" />
```

- Windows only: the package renders through Regira.Drawing.GDI (GDI+), which is Windows-only on .NET 6 and later.
- FreeSpire.PDF is the vendor's free edition. It throws on loading or creating a PDF of more than ten pages, a merge whose result passes ten included, and `ToImages` renders only the first three pages: the images after them are blank.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — the shared interfaces and models, how the backends compare, and which operations Spire leaves out
- [PDF examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/examples.html) — PDF pages to images and printing with Spire

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

The wrapped FreeSpire.PDF library is licensed separately by its vendor; see its [package page](https://www.nuget.org/packages/FreeSpire.PDF).
