# Regira.Drawing.GDI — index card

> The Windows-only `IImageService` backend on GDI+ (`System.Drawing.Common`). `Regira.Drawing.SkiaSharp` is the
> cross-platform, preferred one. It adds no API of its own. The guide lives on the hub `Regira.Media`:
> `get_package(id: "Regira.Media", section: "media.instructions")`, with the recipes in
> `get_package(id: "Regira.Media", section: "media.examples", heading: "Generate a thumbnail")`.

- **Register** `services.AddSingleton<IImageService, Regira.Drawing.GDI.Services.ImageService>()`; `IImageService` is
  in `Regira.Media.Drawing.Services.Abstractions`. The package has no `Add…` extension.
- **Windows only**: `System.Drawing.Common` throws on Linux and macOS. An app that may run in a container uses
  `Regira.Drawing.SkiaSharp`.
- **Every `IImageFile` it returns is disposable**: hold each one in `using var`, the parsed source included, and read
  the result with `GetBytes()` before it is disposed.
- How it differs from `Regira.Drawing.SkiaSharp` (platforms, default resize quality):
  `get_package(id: "Regira.Media", section: "media.instructions", heading: "Backend Comparison")`.
