# Regira.Office.Word.Aspose

Word backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [Aspose.Words](https://www.nuget.org/packages/Aspose.Words). `WordService` implements `IWordService` with the same template features as Word.Spire. It is the one backend that both loads ODT templates and writes EPUB.

## Installation

```xml
<PackageReference Include="Regira.Office.Word.Aspose" Version="6.*" />
```

- An Aspose.Words licence is required: pass it to the constructor in an `AsposeWordConfig` (`LicensePath` or `LicenseBase64`), or set the `ASPOSE_WORDS_LICENSE` or `ASPOSE_WORDS_LICENSE_PATH` environment variable. When none resolves, constructing `WordService` throws unless `AllowEvaluation` is set; evaluation output is watermarked, and long documents are cut short.
- PDF conversion and page images render through SkiaSharp; on Linux, add `SkiaSharp.NativeAssets.Linux` and install `libfontconfig1` and `libharfbuzz-icu0`.

## Documentation

- [Word](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/) — the shared contracts, the template syntax, and how the backends compare
- [Word.Aspose notes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#wordaspose) — registering the licence, evaluation mode, format limits, and Linux rendering
- [Word examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/examples.html) — template filling, conversion, merge and extraction

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

The wrapped Aspose.Words library is licensed separately by its vendor; see its [package page](https://www.nuget.org/packages/Aspose.Words).
