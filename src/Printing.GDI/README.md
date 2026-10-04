# Regira.Printing.GDI

Image printing for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) through GDI+ (`System.Drawing.Printing`). `PrintService` implements `IPrintService`: `List()` returns the installed printers, and `Print(ImagePrintInputModel)` sends one image to the named printer, scaled to fit within the margins. The model also sets copies, collation, page range, duplex, paper size (by name), paper source, orientation, colour and margins. To print PDF files, use an `IPdfPrinter` backend instead.

## Installation

```xml
<PackageReference Include="Regira.Printing.GDI" Version="6.*" />
```

Windows only: GDI+ printing (System.Drawing.Common) is Windows-only on .NET 6 and later.

## Documentation

- [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) — the family this package belongs to; `IPrintService` and `ImagePrintInputModel` ship in its shared abstractions (`Regira.Office.Printing`)
- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — `IPdfPrinter` and the PDF printing backends
- [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/) — the `ImageFile` model the print input carries, and the GDI+ backend (Regira.Drawing.GDI) this package depends on

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
