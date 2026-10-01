# Regira.Drawing.SkiaSharp

Cross-platform image backend for [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/), built on [SkiaSharp](https://www.nuget.org/packages/SkiaSharp). `ImageService` (`Regira.Drawing.SkiaSharp.Services`) implements `IImageService`. It is the preferred of the two backends and runs on Windows, Linux and macOS; `Regira.Drawing.GDI` is the Windows-only alternative.

## Installation

```xml
<PackageReference Include="Regira.Drawing.SkiaSharp" Version="6.*" />
```

The `SkiaSharp` package brings the native library for Windows and macOS only. On Linux, add `SkiaSharp.NativeAssets.Linux` at the version of `SkiaSharp` the application resolves.

## Documentation

- [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/) — `IImageService`, the image models, and layer composition with `ImageBuilder`
- [SkiaSharp vs GDI](https://regira.github.io/Regira-Packages/src/Common.Media/#skiasharp-vs-gdi) — where the two backends differ: platforms, default resize quality, printing
- [Drawing examples](https://regira.github.io/Regira-Packages/src/Common.Media/docs/examples.html) — thumbnail, watermark, badge builder and an API service

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
