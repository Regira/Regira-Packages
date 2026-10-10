# Regira.Office.PDF.Gotenberg

HTML→PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) that renders through the Chromium route of a [Gotenberg](https://gotenberg.dev) server. `PdfService` implements `IHtmlToPdfService` and applies every `HtmlInput` setting — page size, orientation, margins, header and footer — as PDF.MsPlaywright does; `AddGotenbergPdf` registers it on a named `HttpClient`. It is the route to HTML→PDF without a browser in the application: for an image that must stay slim or may download nothing at run time, or a deployment that already runs Gotenberg for Regira.Office.Word.Gotenberg.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.Gotenberg" Version="6.*" />
```

A running Gotenberg server (a Docker image bundling Chromium and LibreOffice), reachable at `GotenbergPdfConfig.BaseUrl`. The HTML renders in the fonts installed in that image, and an absolute URL in it is fetched from the server's network.

## Documentation

- [Gotenberg notes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/#gotenberg--htmlpdf-without-a-browser-in-the-application) — fonts, URLs, errors, starting a server, and registration
- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — `IHtmlToPdfService`, the `HtmlInput` model, and how the headless-Chromium backends place a header and footer

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
