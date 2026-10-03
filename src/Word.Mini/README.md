# Regira.Office.Word.Mini

Lightweight Word backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [MiniWord](https://www.nuget.org/packages/MiniWord). `WordService` implements `IWordCreator`, `IWordTextExtractor` and `IWordImageExtractor` (`WordCreator` is an `[Obsolete]` alias): it fills templates and extracts text and images, with no vendor licence and no document size cap. MiniWord has no layout engine, so conversion, merging and page images are unavailable — pair it with Word.Gotenberg for PDF output and page images.

## Installation

```xml
<PackageReference Include="Regira.Office.Word.Mini" Version="6.*" />
```

Templates follow MiniWord's own tag syntax, which differs from the other backends' for collection tables and images. Nested documents, headers, footers and non-default `InputOptions` throw `NotSupportedException`.

## Documentation

- [Word](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/) — the shared contracts, the template syntax, and how the backends compare
- [Word.Mini notes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#wordmini) — MiniWord's template syntax and what `Create` supports
- [Fill a template and convert it to PDF without a vendor licence](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/examples.html#example-8-fill-a-template-and-convert-it-to-pdf-without-a-vendor-licence) — Word.Mini together with Word.Gotenberg

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
