# Regira.Drawing.SkiaSharp — index card

> The cross-platform `IImageService` backend (Windows, Linux, macOS) on Google Skia, and the preferred one of the two.
> It adds no API of its own. The guide lives on the hub `Regira.Media`:
> `get_package(id: "Regira.Media", section: "media.instructions")`, with the recipes in
> `get_package(id: "Regira.Media", section: "media.examples", heading: "Generate a thumbnail")`.

- **Register** `services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>()`;
  `IImageService` is in `Regira.Media.Drawing.Services.Abstractions`. The package has no `Add…` extension.
- **Every `IImageFile` it returns is disposable**: hold each one in `using var`, the parsed source included, and read
  the result with `GetBytes()` before it is disposed.
- **Linux needs the native library**: add `SkiaSharp.NativeAssets.Linux` (or `.NoDependencies`) at the version of
  `SkiaSharp` the app resolves. The `SkiaSharp` package brings it for Windows and macOS only.
- **Text images draw one line**: in `CreateTextImage`, a `\n` in `LabelImageOptions.Text` renders as a glyph, so
  compose one text layer per line.
- How it differs from `Regira.Drawing.GDI` (platforms, default resize quality):
  `get_package(id: "Regira.Media", section: "media.instructions", heading: "Backend Comparison")`.
