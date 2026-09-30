# Regira.Office.Word.Syncfusion

Word backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [Syncfusion DocIO](https://www.nuget.org/packages/Syncfusion.DocIO.Net.Core). `WordService` implements `IWordService` with the same template features as Word.Spire and no document size cap. ODT templates cannot be loaded and EPUB export is unavailable.

## Installation

```xml
<PackageReference Include="Regira.Office.Word.Syncfusion" Version="6.*" />
```

- A Syncfusion licence key is required: pass it to the constructor in a `SyncfusionWordConfig`, or set the `SYNCFUSION_LICENSE_KEY` environment variable. Without a valid key DocIO adds trial text to every document it produces.
- PDF conversion and page images render through SkiaSharp and HarfBuzzSharp; on Linux, add `SkiaSharp.NativeAssets.Linux` and `HarfBuzzSharp.NativeAssets.Linux`.

## Documentation

- [Word](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/) — the shared contracts, the template syntax, and how the backends compare
- [Word.Syncfusion notes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#wordsyncfusion) — registering the licence key, format limits, and the Linux rendering packages
- [Word examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/examples.html) — template filling, conversion, merge and extraction

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

The wrapped Syncfusion DocIO library is licensed separately by its vendor; see its [package page](https://www.nuget.org/packages/Syncfusion.DocIO.Net.Core).
