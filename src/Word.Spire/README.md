# Regira.Office.Word.Spire

Word backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [FreeSpire.Doc](https://www.nuget.org/packages/FreeSpire.Doc). `WordService` implements `IWordService`: filling templates, conversion, merging, text and image extraction, and page images (`WordManager` is an `[Obsolete]` alias). It is the recommended Word backend, within the limits of the FreeSpire.Doc free edition.

## Installation

```xml
<PackageReference Include="Regira.Office.Word.Spire" Version="6.*" />
```

The FreeSpire.Doc free edition supports documents up to 500 paragraphs or 25 tables, and throws `SpireDocFreeException` past either. It writes at most three pages of PDF, without raising an error; the other output formats and page images carry every page. With a Spire.Doc licence, the same package runs on the commercial [Spire.Doc](https://www.nuget.org/packages/Spire.Doc) instead — see [Commercial Spire.Doc](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#commercial-spiredoc).

## Documentation

- [Word](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/) — the shared contracts, the template syntax, and how the backends compare
- [Word.Spire notes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#wordspire-recommended) — HTML parameters, output formats, and the free-edition limits
- [Word examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/examples.html) — template filling, conversion, merge and extraction

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

The wrapped FreeSpire.Doc library is licensed separately by its vendor; see its [package page](https://www.nuget.org/packages/FreeSpire.Doc).
