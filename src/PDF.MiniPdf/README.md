# Regira.Office.PDF.MiniPdf

PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [MiniPdf](https://www.nuget.org/packages/MiniPdf). `PdfService` implements `IDocumentToPdfService`: it converts Word documents (`.docx`), spreadsheets (`.xlsx`) and presentations (`.pptx`) to PDF in-process, with no Office installation, server, browser or licence.

MiniPdf lays documents out itself, so a complex layout comes out less faithful than through LibreOffice or Word. For a Word document that must match Word's layout, use a Word backend's `IWordConverter`.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.MiniPdf" Version="6.*" />
```

`PdfService` has no constructor dependencies. Text renders in the host's system fonts; on a host with few, such as a container, register TrueType fonts once at startup with `MiniSoftware.MiniPdf.RegisterFont`.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — the shared interfaces and models, how the backends compare, and MiniPdf's implementation notes: which page settings each source format takes
- [PDF examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/examples.html) — converting a spreadsheet to PDF

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). MiniPdf itself is Apache-2.0 licensed. A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
