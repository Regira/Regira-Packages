# Regira.Office.PDF.PockyBum522

PDF printing backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [SimpleFreePdfPrinter](https://www.nuget.org/packages/SimpleFreePdfPrinter). `PdfPrinter` implements `IPdfPrinter`: it lists the installed printers, reports the default one, and prints the PDF of a `PdfPrinterInput` to the printer it names, or to the default printer when it names none.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.PockyBum522" Version="6.*" />
```

Windows only: the package targets `net8.0-windows` and `net10.0-windows`.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — `IPdfPrinter`, the `PdfPrinterInput` model, and the other printing backends
- [PDF examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/examples.html) — listing printers and printing a PDF through `IPdfPrinter`

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
