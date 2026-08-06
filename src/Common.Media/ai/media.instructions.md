# Regira Media (Drawing) AI Agent Instructions

> A cross-platform image processing library with a single `IImageService` interface backed by SkiaSharp (recommended) or GDI+ (Windows-only).

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.Media` | *(transitive)* | Shared abstractions, models, DTOs, and `ImageBuilder` |
| `Drawing.SkiaSharp` | `Regira.Drawing.SkiaSharp` | **Preferred** — cross-platform (SkiaSharp) |
| `Drawing.GDI` | `Regira.Drawing.GDI` | Windows-only alternative (GDI+) |

---

## Installation

```xml
<!-- Preferred — cross-platform (SkiaSharp) -->
<PackageReference Include="Regira.Drawing.SkiaSharp" Version="6.*" />

<!-- Windows-only alternative (GDI+) -->
<PackageReference Include="Regira.Drawing.GDI" Version="6.*" />
```

---

## Backend Comparison

| Feature | `Drawing.SkiaSharp` | `Drawing.GDI` |
|---|---|---|
| **Recommended** | ✓ | – |
| **Cross-platform** | ✓ (Win / Linux / macOS) | Windows only |
| **Default resize quality** | 80 | 100 |
| **EXIF auto-rotate** | – | ✓ |
| **Printing support** | – | ✓ (`PrintUtility`) |
| **Engine** | Google Skia | GDI+ (`System.Drawing.Common`) |

Both implement `IImageService` and are interchangeable.

---

## Core Models

### `IImageFile` / `ImageFile`

Represents an image in memory. Implements `IDisposable`.

| Property | Type | Description |
|---|---|---|
| `Bytes` | `byte[]?` | Raw encoded image bytes |
| `Stream` | `Stream?` | Stream-based access |
| `Size` | `ImageSize?` | Width × height |
| `Format` | `ImageFormat?` | Detected or set format |
| `ContentType` | `string?` | MIME type |

### `ImageSize`

```csharp
var size   = new ImageSize(800, 600);
var half   = size / 2;          // (400, 300)
var square = (ImageSize)128;    // (128, 128) — implicit from int
```

| Member | Description |
|---|---|
| `Width`, `Height` | Integer dimensions |
| `Empty` | `(0, 0)` sentinel |
| `*`, `/` operators | Scale by factor |
| Implicit from `int` | Creates a square |
| Implicit from `int[]` | `[width, height]` |

### `Color`

RGBA struct with hex string support.

```csharp
Color c = "#FF000080";  // implicit from string
string rgb  = c.Hex;    // "FF0000"
string rgba = c.HexA;   // "FF000080"
```

Static constants: `Color.White`, `Color.Black`, `Color.Transparent`

Formats: `#RGB`, `#RRGGBB`, `#RRGGBBAA`

### `ImageFormat`

```
Png  Jpeg  Webp  Gif  Bmp  Tiff  Ico  Heif  Tga  Wbmp  …
```

### `ImagePosition`

Flags enum for layer alignment — combine with `|`:

```
Absolute   Left   Right   Top   Bottom   HCenter   VCenter
```

### `ImageEdgeOffset`

CSS-style distance from each edge:

```csharp
new ImageEdgeOffset(top: 10, left: 20, bottom: 10, right: 20)
new ImageEdgeOffset(10, 20)   // top + left only
```

### `ImageLayerOptions`

Controls positioning when compositing layers.

| Property | Type | Default | Description |
|---|---|---|---|
| `Size` | `ImageSize?` | *(natural)* | Override layer dimensions |
| `Margin` | `int` | `0` | Inset from position anchor |
| `Position` | `ImagePosition` | `Absolute` | Alignment within canvas |
| `Offset` | `ImageEdgeOffset?` | `(0, 0)` | Pixel offset for `Absolute` |
| `Rotation` | `int` | `0` | Clockwise degrees |
| `Opacity` | `float` | `1.0` | 0 = invisible, 1 = opaque |

---

## IImageService — Image Operations

`IImageService` is a composite of five sub-interfaces.

### Parsing

```csharp
Task<IImageFile?> Parse(Stream? stream)
Task<IImageFile?> Parse(byte[]? bytes)
Task<IImageFile?> Parse(byte[] rawBytes, ImageSize size, ImageFormat? format = null)
Task<IImageFile?> Parse(IMemoryFile file)
```

### Format

```csharp
Task<ImageFormat> GetFormat(IImageFile input)
Task<IImageFile>  ChangeFormat(IImageFile input, ImageFormat targetFormat)
```

### Transform

```csharp
Task<ImageSize>  GetDimensions(IImageFile input)
Task<IImageFile> Resize(IImageFile input, ImageSize wantedSize, int quality = 100)       // preserves aspect ratio
Task<IImageFile> ResizeFixed(IImageFile input, ImageSize size, int quality = 100)        // ignores aspect ratio
Task<IImageFile> CropRectangle(IImageFile input, ImageEdgeOffset rect)
Task<IImageFile> Rotate(IImageFile input, int degrees, Color? background = null)
Task<IImageFile> FlipHorizontal(IImageFile input)
Task<IImageFile> FlipVertical(IImageFile input)
```

> SkiaSharp default quality: 80. GDI default quality: 100.

### Color

```csharp
Task<Color>      GetPixelColor(IImageFile input, int x, int y)
Task<IImageFile> MakeTransparent(IImageFile input, Color? color = null)  // null = auto-detect background
Task<IImageFile> MakeOpaque(IImageFile input)
```

### Draw / Create

```csharp
Task<IImageFile> Create(ImageSize size, Color? backgroundColor = null, ImageFormat? format = null)
Task<IImageFile> CreateTextImage(LabelImageOptions? options = null)
Task<IImageFile> Draw(IEnumerable<ImageLayer> items, IImageFile? target = null)
```

---

## Layer Composition — `ImageBuilder`

Fluent API for compositing multiple layers onto a single canvas.

### DI Registration

```csharp
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

```csharp
var result = await new ImageBuilder(imageService, imageCreators)
    .SetBaseLayer(new CanvasImageOptions { Size = new ImageSize(800, 600), BackgroundColor = Color.White })
    .Add(layer1, layer2, layer3)
    .Build();
```

### `SetBaseLayer` overloads

| Overload | Description |
|---|---|
| `SetBaseLayer(IImageFile target)` | Existing image as canvas |
| `SetBaseLayer(CanvasImageOptions options)` | Create a blank canvas |
| `SetBaseLayer(IImageLayer layer)` | Any resolved `IImageLayer` |

If no base layer is set, `Build()` auto-calculates a canvas that fits all added layers.

### Layer types

```csharp
// Existing image — pin to bottom-right
new ImageLayer {
    Source  = imageFile,
    Options = new() { Position = ImagePosition.Right | ImagePosition.Bottom, Margin = 10 }
}

// Blank colored rectangle — absolute position
new ImageLayer<CanvasImageOptions> {
    Source  = new() { Size = new ImageSize(100, 30), BackgroundColor = "#0000FF80" },
    Options = new() { Offset = new ImageEdgeOffset(top: 20, left: 15) }
}

// Text label — centered with rotation and opacity
new ImageLayer<LabelImageOptions> {
    Source  = new() { Text = "DRAFT", FontSize = 32, TextColor = "#FF0000",
                      BackgroundColor = Color.Transparent },
    Options = new() { Position = ImagePosition.HCenter | ImagePosition.VCenter,
                      Rotation = -30, Opacity = 0.4f }
}
```

### Custom `IImageCreator`

Derive from `ImageCreatorBase<T>` and override the **async** `Create`. The input type `T` is what an `ImageLayer<T>.Source` carries; the builder routes each layer to the first creator whose `CanCreate` returns true.

```csharp
public abstract Task<IImageFile?> Create(T input, CancellationToken cancellationToken = default);
```

A real example ships in `Regira.Office.Barcodes` — it bridges a barcode/QR writer into the layer system, so a `BarcodeInput` can be added as an `ImageLayer`:

```csharp
// Regira.Office.Barcodes.Drawing.BarcodeImageCreator
public class BarcodeImageCreator(IBarcodeWriter barcodeWriter) : ImageCreatorBase<BarcodeInput>
{
    public override async Task<IImageFile?> Create(BarcodeInput input, CancellationToken cancellationToken = default)
        => await barcodeWriter.Create(input);
}

services.AddSingleton<IImageCreator, BarcodeImageCreator>();
```

---

## Text Images

```csharp
using var img = await imageService.CreateTextImage("Hello World");  // string converts implicitly to LabelImageOptions
```

| Property | Type | Default | Description |
|---|---|---|---|
| `Text` | `string` | *(required)* | Content to render |
| `FontName` | `string?` | `"Arial"` | Font family |
| `FontSize` | `int?` | `15` | Size in points |
| `Padding` | `int?` | `0` | Padding in pixels |
| `TextColor` | `Color?` | `#000000FF` | Foreground color |
| `BackgroundColor` | `Color?` | `#FFFFFFFF` | Background fill |

Use `Color.Transparent` as background when compositing over another image.

---

## Simple DI Registration

```csharp
// SkiaSharp (recommended)
services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();

// GDI (Windows only)
services.AddSingleton<IImageService, Regira.Drawing.GDI.Services.ImageService>();
```

---

## Quick Example

```csharp
using var image   = await imageService.Parse(inputBytes);
using var resized = await imageService.Resize(image!, new ImageSize(200, 200));
using var webp    = await imageService.ChangeFormat(resized, ImageFormat.Webp);
return webp.GetBytes()!;
```
