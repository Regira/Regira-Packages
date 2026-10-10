# Regira.Office.PDF.SelectPdf

HTML→PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [Select.HtmlToPdf.NetCore](https://www.nuget.org/packages/Select.HtmlToPdf.NetCore). `PdfManager` implements `IHtmlToPdfService`. It applies every `HtmlInput` property — page size, orientation, margins, headers and footers — and needs no browser installation.

Two limits decide whether it fits:

- **Windows only.** It renders through `System.Drawing.Common`, which throws on other platforms.
- **Five pages.** The free Community Edition of Select.HtmlToPdf converts only the first five pages' worth of a document. The rest is left out of the PDF, without an error or a notice. A longer document needs the vendor's paid edition or a headless-Chromium backend.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.SelectPdf" Version="6.*" />
```

SelectPdf needs its native companion file `Select.Html.dep` beside the application. The package's build targets copy it from the restored Select.HtmlToPdf.NetCore package into the build and publish output; set the MSBuild property `RegiraSelectPdfCopyNativeDep` to `false` to place it yourself.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — `IHtmlToPdfService`, the `HtmlInput` model, and how the HTML→PDF backends compare
- [PDF examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/examples.html) — HTML to PDF with a header and footer, and a template-to-PDF pipeline

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

The wrapped Select.HtmlToPdf library is licensed separately by its vendor; see its [package page](https://www.nuget.org/packages/Select.HtmlToPdf.NetCore).
