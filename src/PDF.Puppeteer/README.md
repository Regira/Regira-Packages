# Regira.Office.PDF.Puppeteer

HTML→PDF backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) that renders through headless Chromium with [PuppeteerSharp](https://www.nuget.org/packages/PuppeteerSharp), for pages whose CSS needs a browser engine. `PdfManager` implements `IHtmlToPdfService` and applies every `HtmlInput` setting — page size, orientation, margins, header and footer — on Windows, Linux and macOS, with no page limit, as PDF.MsPlaywright does.

## Installation

```xml
<PackageReference Include="Regira.Office.PDF.Puppeteer" Version="6.*" />
```

Chromium is downloaded on first use, so the first conversion needs network access and writable disk at run time, or a browser cache seeded in advance.

## Documentation

- [PDF](https://regira.github.io/Regira-Packages/src/Common.Office/docs/pdf/) — `IHtmlToPdfService`, the `HtmlInput` model, and how the headless-Chromium backends place a header and footer

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
