# Regira Drawing

Regira Drawing is a .NET image processing library that provides a **consistent abstraction** over image manipulation, format conversion, and multi-layer composition. All operations are available through a single `IImageService` interface, implemented by both backends.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.Media` | `Regira.Media` | Shared abstractions, models, DTOs, and `ImageBuilder` |
| `Drawing.SkiaSharp` | `Regira.Drawing.SkiaSharp` | **Preferred** — cross-platform (SkiaSharp) |
| `Drawing.GDI` | `Regira.Drawing.GDI` | Windows-only alternative (GDI+) |

## Installation

```xml
<!-- Preferred (cross-platform) -->
<PackageReference Include="Regira.Drawing.SkiaSharp" Version="6.*" />

<!-- Windows-only alternative -->
<PackageReference Include="Regira.Drawing.GDI" Version="6.*" />
```

## Quick Start

```csharp no-compile
// Register
services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();

// Use
using var image   = (await imageService.Parse(inputBytes))!;
using var resized = await imageService.Resize(image, new ImageSize(200, 200));
using var webp    = await imageService.ChangeFormat(resized, ImageFormat.Webp);
return webp.GetBytes()!;
```

## Core Models

### IImageFile / ImageFile

Represents an image held in memory. Implements `IDisposable`.

| Property | Type | Description |
|----------|------|-------------|
| `Bytes` | `byte[]?` | Raw encoded image bytes |
| `Stream` | `Stream?` | Stream-based access |
| `Size` | `ImageSize?` | Width × height |
| `Format` | `ImageFormat?` | Detected or set format |
| `ContentType` | `string?` | MIME type |

### ImageSize

```csharp
var size   = new ImageSize(800, 600);
var half   = size / 2;          // (400, 300)
var square = (ImageSize)128;    // (128, 128) — implicit from int
```

| Member | Description |
|--------|-------------|
| `Width`, `Height` | Integer dimensions |
| `Empty` | `(0, 0)` sentinel |
| `*`, `/` operators | Scale by integer factor |
| Implicit from `int` | Creates a square of that side |
| Implicit from `int[]` | `[width, height]` |

### Color

RGBA struct with hex string support.

| Format | Example | Alpha |
|--------|---------|-------|
| `#RGB` | `#F00` | 255 (opaque) |
| `#RGBA` | `#F008` | from hex |
| `#RRGGBB` | `#FF0000` | 255 (opaque) |
| `#RRGGBBAA` | `#FF000080` | from hex |
| Static constants | `Color.White`, `Color.Black`, `Color.Transparent` | |

```csharp
Color c = "#FF000080";   // implicit from string
string rgb  = c.Hex;    // "#FF0000"
string rgba = c.HexA;   // "#FF000080"
```

### ImageFormat

```
Png  Jpeg  Webp  Gif  Bmp  Tiff  Ico  Heif  Tga  Wbmp  …
```

### ImageEdgeOffset

CSS-style distance from each edge.

```csharp no-compile
new ImageEdgeOffset(top: 10, left: 20, bottom: 10, right: 20)
new ImageEdgeOffset(10, 20)   // top + left; Bottom and Right stay null
```

### ImagePosition

Flags enum for layer alignment. Combine with `|`.

| Value | Description |
|-------|-------------|
| `Absolute` | Use `Offset` coordinates directly |
| `Left` / `Right` | Horizontal edge alignment |
| `Top` / `Bottom` | Vertical edge alignment |
| `HCenter` | Horizontal center |
| `VCenter` | Vertical center |

### ImageLayerOptions

Controls how a layer is positioned and rendered when composited.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Size` | `ImageSize?` | *(natural size)* | Override layer dimensions |
| `Margin` | `int` | `0` | Inset from the position anchor |
| `Position` | `ImagePosition` | `Absolute` | Alignment within the canvas |
| `Offset` | `ImageEdgeOffset?` | `(0, 0)` | Pixel offset for `Absolute` positioning |
| `Rotation` | `int` | `0` | Clockwise rotation in degrees |
| `Opacity` | `float` | `1.0` | Transparency (0 = invisible, 1 = opaque) |

## IImageService — Image Operations

`IImageService` is a composite of five focused sub-interfaces. Every method returns a `Task` and accepts an optional trailing `CancellationToken` (omitted below for brevity).

### Parsing

```csharp no-compile
Task<IImageFile?> Parse(Stream? stream)
Task<IImageFile?> Parse(byte[]? bytes)
Task<IImageFile?> Parse(byte[] rawBytes, ImageSize size, ImageFormat? format = null)
Task<IImageFile?> Parse(IMemoryFile file)
```

The third overload accepts unencoded pixel data together with explicit dimensions and format.

### Format

```csharp no-compile
Task<ImageFormat> GetFormat(IImageFile input)
Task<IImageFile>  ChangeFormat(IImageFile input, ImageFormat targetFormat)
```

### Transform

```csharp no-compile
Task<ImageSize>  GetDimensions(IImageFile input)
Task<IImageFile> Resize(IImageFile input, ImageSize wantedSize, int quality = 100)     // preserves aspect ratio
Task<IImageFile> ResizeFixed(IImageFile input, ImageSize size, int quality = 100)      // ignores aspect ratio
Task<IImageFile> CropRectangle(IImageFile input, ImageEdgeOffset rect)
Task<IImageFile> Rotate(IImageFile input, int degrees, Color? background = null)
Task<IImageFile> FlipHorizontal(IImageFile input)
Task<IImageFile> FlipVertical(IImageFile input)
```

> **SkiaSharp default quality:** 80. **GDI default quality:** 100.

### Color

```csharp no-compile
Task<Color>      GetPixelColor(IImageFile input, int x, int y)
Task<IImageFile> MakeTransparent(IImageFile input, Color? color = null)  // null = light gray (245, 245, 245)
Task<IImageFile> MakeOpaque(IImageFile input)
```

### Draw / Create

```csharp no-compile
Task<IImageFile> Create(ImageSize size, Color? backgroundColor = null, ImageFormat? format = null)
Task<IImageFile> CreateTextImage(LabelImageOptions? options = null)
Task<IImageFile> Draw(IEnumerable<ImageLayer> items, IImageFile? target = null)
```

## Layer Composition — ImageBuilder

`ImageBuilder` composes multiple layers onto a single canvas using a fluent API.

### Registration

```csharp no-compile
services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();
services.AddSingleton<IImageCreator, CanvasImageCreator>();
services.AddSingleton<IImageCreator, LabelImageCreator>();
services.AddSingleton<IImageCreator>(provider =>
    AggregateImageCreator.Create(
        provider.GetRequiredService<IImageService>(),
        provider.GetServices<IImageCreator>()
    )
);
```

### Fluent API

```csharp no-compile
var result = await new ImageBuilder(imageService, imageCreators)
    .SetBaseLayer(new CanvasImageOptions { Size = new ImageSize(800, 600), BackgroundColor = Color.White })
    .Add(layer1, layer2, layer3)
    .Build();
```

### SetBaseLayer overloads

| Overload | Description |
|----------|-------------|
| `SetBaseLayer(IImageFile target)` | Existing image as canvas |
| `SetBaseLayer(CanvasImageOptions options)` | Create a blank canvas |
| `SetBaseLayer(IImageLayer layer)` | Any resolved `IImageLayer` |

If no base layer is set, `Build()` auto-calculates a canvas that fits all added layers.

### Layer types

Three generic types let you add image files, canvases, or labels as layers:

```csharp no-compile
// Existing image — pin to bottom-right
new ImageLayer { Source = imageFile,
                 Options = new() { Position = ImagePosition.Right | ImagePosition.Bottom, Margin = 10 } }

// Blank colored rectangle — absolute position
new ImageLayer<CanvasImageOptions> { Source = new() { Size = new ImageSize(100, 30), BackgroundColor = "#0000FF80" },
                                     Options = new() { Offset = new ImageEdgeOffset(top: 20, left: 15) } }

// Text label — centered with rotation and opacity
new ImageLayer<LabelImageOptions>  { Source = new() { Text = "DRAFT", FontSize = 32, TextColor = "#FF0000",
                                                       BackgroundColor = Color.Transparent },
                                     Options = new() { Position = ImagePosition.HCenter | ImagePosition.VCenter,
                                                       Rotation = -30, Opacity = 0.4f } }
```

### Custom IImageCreator

Implement `IImageCreator<T>` to make `ImageBuilder` understand any source type:

```csharp no-compile
public class QrCodeCreator(IQrService qr) : ImageCreatorBase<QrCodeOptions>
{
    public override async Task<IImageFile?> Create(QrCodeOptions input, CancellationToken cancellationToken = default) =>
        new ImageFile { Bytes = await qr.Generate(input.Content, input.Size), Format = ImageFormat.Png };
}

services.AddSingleton<IImageCreator, QrCodeCreator>();
```

## Text Images

```csharp
IImageService imageService = new Regira.Drawing.SkiaSharp.Services.ImageService();

using var img = await imageService.CreateTextImage("Hello World");   // implicit string shorthand
```

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Text` | `string` | *(required)* | Content to render |
| `FontName` | `string?` | `"Arial"` | Font family |
| `FontSize` | `int?` | `15` | Size in points |
| `Padding` | `int?` | `0` | Padding in pixels |
| `TextColor` | `Color?` | `#000000FF` | Foreground color |
| `BackgroundColor` | `Color?` | `#FFFFFFFF` | Background fill |

> Use `Color.Transparent` as background when compositing the label over another image.

## SkiaSharp vs GDI

| Feature | `Drawing.SkiaSharp` | `Drawing.GDI` |
|---------|---------------------|---------------|
| **Recommended** | ✓ | – |
| **Cross-platform** | ✓ (Win / Linux / macOS) | Windows only |
| **Default resize quality** | 80 | 100 |
| **EXIF auto-rotate** | – | ✓ |
| **Printing support** | – | ✓ (`PrintUtility`) |
| **Engine** | Google Skia | GDI+ (`System.Drawing.Common`) |

Both implement `IImageService` and are interchangeable in consuming code.

## DTOs & API Integration

`Common.Media` ships DTO types for JSON API contracts. `DtoExtensions` converts them to domain objects.

| DTO | Description |
|-----|-------------|
| `ImageLayerDto` | Image bytes + draw options |
| `ImageLayerOptionsDto` | Draw options: unit, size, position, rotation, opacity |
| `CanvasImageDto` | Blank canvas definition |
| `CanvasImageLayerDto` | Canvas with draw positioning |
| `LabelImageLayerDto` | Text content + label style + draw options |

All measurement properties (`Width`, `Height`, `Top`, `Left`, …) are `float` and interpreted according to the DTO's `DimensionUnit` property, of type `LengthUnit`:

| `LengthUnit` | Description |
|--------------|-------------|
| `Points` | Points / pixels (default) |
| `Inches` | Physical inches |
| `Millimeters` | Physical millimeters |
| `Percent` | Relative to canvas size |

A live demo is available at [services.regira.com/office](https://services.regira.com/office/index.html) — endpoint `/drawing/create`, samples at `/drawing/samples/**`.

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.Media/)** — Overview, models, and API reference
1. [Examples](https://regira.github.io/Regira-Packages/src/Common.Media/docs/examples.html) — Thumbnail, watermark, badge builder, and API service pattern
1. [Video processing](https://regira.github.io/Regira-Packages/src/Common.Media/docs/video.html) — Video compression and snapshot extraction via FFMpeg
