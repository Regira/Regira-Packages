# Regira.Drawing.GDI

GDI+ image backend for [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/), built on [System.Drawing.Common](https://www.nuget.org/packages/System.Drawing.Common). `ImageService` (`Regira.Drawing.GDI.Services`) implements `IImageService`. It is the Windows-only alternative to the preferred, cross-platform `Regira.Drawing.SkiaSharp`, and the only backend with printing support (`PrintUtility`).

## Installation

```xml
<PackageReference Include="Regira.Drawing.GDI" Version="6.*" />
```

Windows only: GDI+ (`System.Drawing.Common`) is Windows-only on .NET 6 and later, and the assembly is marked `[SupportedOSPlatform("windows")]`.

## Documentation

- [Regira Drawing](https://regira.github.io/Regira-Packages/src/Common.Media/) — `IImageService`, the image models, and layer composition with `ImageBuilder`
- [SkiaSharp vs GDI](https://regira.github.io/Regira-Packages/src/Common.Media/#skiasharp-vs-gdi) — where the two backends differ: platforms, default resize quality, printing
- [Drawing examples](https://regira.github.io/Regira-Packages/src/Common.Media/docs/examples.html) — thumbnail, watermark, badge builder and an API service

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
