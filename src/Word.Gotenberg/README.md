# Regira.Office.Word.Gotenberg

Word backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) that converts through the LibreOffice route of a [Gotenberg](https://gotenberg.dev) server. `WordService` implements `IWordConverter`, producing PDF only, and `IWordToImagesService`; `AddGotenbergWord` registers both on one named `HttpClient`. It is the route to PDF output and page images without a vendor licence.

## Installation

```xml
<PackageReference Include="Regira.Office.Word.Gotenberg" Version="6.*" />
```

- A running Gotenberg server (a Docker image bundling LibreOffice and Chromium), reachable at `GotenbergWordConfig.BaseUrl`.
- Two optional collaborators, passed to the constructor or taken from the container: an `IWordCreator` (Regira.Office.Word.Mini, for example) to fill template input before conversion, and an `IPdfToImageService` (Regira.Office.PDF.DocNET, for example) for page images.

## Documentation

- [Word.Gotenberg notes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#wordgotenberg) — source formats, templates, page settings, page images, starting a server, and registration
- [Fill a template and convert it to PDF without a vendor licence](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/examples.html#example-8-fill-a-template-and-convert-it-to-pdf-without-a-vendor-licence) — Word.Gotenberg together with Word.Mini
- [Word](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/) — the shared contracts and how the backends compare

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
