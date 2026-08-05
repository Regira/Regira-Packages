# Regira Barcodes

Regira Barcodes provides a **unified abstraction** for generating and reading barcodes and QR codes across multiple underlying libraries. All implementations share the same `IBarcodeService` / `IQRCodeService` interfaces.

## Projects

| Project | Package | Backend | Formats | Read | Write | Cross-platform |
|---------|---------|---------|---------|------|-------|----------------|
| `Common.Office` | *(transitive)* | Shared abstractions | — | — | — | — |
| `Barcodes.ZXing` | `Regira.Office.Barcodes.ZXing` | ZXing.Net (SkiaSharp) | All 13 | ✓ | ✓ | ✓ |
| `Barcodes.Spire` | `Regira.Office.Barcodes.Spire` | FreeSpire.Barcode (GDI+) | All 13 | ✓ | ✓ | Windows only |
| `Barcodes.QRCoder` | `Regira.Office.Barcodes.QRCoder` | QRCoder (GDI+) | QR only | — | ✓ | Windows only |
| `Barcodes.UziGranot` | `Regira.Office.Barcodes.UziGranot` | Embedded CPOL (GDI+) | QR only | ✓ | ✓ | Windows only |

Only **ZXing** is cross-platform. Spire, QRCoder, and UziGranot render through `Regira.Drawing.GDI` / `System.Drawing` (GDI+), which is Windows-only.

## Installation

```xml
<!-- ZXing — recommended (all formats, cross-platform) -->
<PackageReference Include="Regira.Office.Barcodes.ZXing" Version="6.*" />

<!-- Spire — all formats, Windows only -->
<PackageReference Include="Regira.Office.Barcodes.Spire" Version="6.*" />

<!-- QRCoder — write-only QR -->
<PackageReference Include="Regira.Office.Barcodes.QRCoder" Version="6.*" />

<!-- UziGranot — embedded QR, no external dependency -->
<PackageReference Include="Regira.Office.Barcodes.UziGranot" Version="6.*" />
```

## Quick Start

```csharp
// Generate a QR code
IQRCodeService qr = new Regira.Office.Barcodes.ZXing.QRCodeService();
IImageFile img = await qr.Create("https://example.com");

// Generate a Code128 barcode
IBarcodeService bc = new Regira.Office.Barcodes.ZXing.BarcodeService();
IImageFile barcode = await bc.Create(new BarcodeInput { Content = "ABC-1234" });

// Read / scan
BarcodeReadResult? result = await bc.Read(barcode);
Console.WriteLine(result?.Contents?[0]);   // "ABC-1234"
```

> **Output format:** all backends encode the result as **JPEG** regardless of the requested format. Save with a `.jpg` extension, or re-encode via `IImageService.ChangeFormat`.

## Interfaces

### IBarcodeWriter

```csharp no-compile
Task<IImageFile> Create(BarcodeInput input, CancellationToken cancellationToken = default);
```

### IBarcodeReader

```csharp no-compile
Task<BarcodeReadResult?> Read(IImageFile img, BarcodeFormat? format = null, CancellationToken cancellationToken = default);
```

Pass `format` to narrow the scanner to a specific type; pass `null` (the default) to try all supported formats. Note that passing `BarcodeFormat.Any` is **not** the same as `null`: ZXing maps `Any` to its `All_1D` set, so 2D symbologies (QR, DataMatrix, PDF417, Aztec) are then excluded.

### IQRCodeWriter / IQRCodeReader

```csharp no-compile
Task<IImageFile>          Create(QRCodeInput input, CancellationToken cancellationToken = default);
Task<BarcodeReadResult?>  Read(IImageFile qrCode, CancellationToken cancellationToken = default);
```

### IBarcodeService / IQRCodeService

Composite interfaces that combine read + write. Use these as the injection target.

## Input Models

### BarcodeInput

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Content` | `string` | *(required)* | Data to encode |
| `Format` | `BarcodeFormat` | `Code128` | Barcode symbology |
| `Size` | `ImageSize` | `400 × 100` | Output image dimensions |
| `Color` | `Color` | `Black` | Bar / module colour |
| `BackgroundColor` | `Color` | `White` | Background colour |

Implicit conversion from `string` creates a `BarcodeInput` with default options:

```csharp
BarcodeInput input = "ABC-1234";
```

### QRCodeInput

Inherits `BarcodeInput`. Format is locked to `QRCode`. Default size is square (`Width × Width`).

```csharp
QRCodeInput qr = "https://example.com";   // implicit
var custom = new QRCodeInput { Content = "Hello", Size = new ImageSize(300, 300) };
```

## Output — BarcodeReadResult

| Property | Type | Description |
|----------|------|-------------|
| `Format` | `BarcodeFormat?` | Detected symbology |
| `Contents` | `string[]?` | Decoded values (some implementations can detect multiple codes in one image) |

## BarcodeFormat

Flags enum — can be combined with `|`. `Any` is the combination of all 13 symbology flags, but the ZXing reader maps it to its 1D-only set (`All_1D`) — to scan every symbology pass `format: null` instead. `UnKnown = 0` is what a read returns when the detected symbology has no mapping.

```
UnKnown = 0
Code39  Code93  Code128  CodaBar  DataMatrix
Ean8    Ean13   Itf      Upca     Upce
QRCode  Pdf417  Aztec    Any
```

## Implementation notes

### ZXing (recommended)

Uses SkiaSharp — cross-platform. Full 13-format bidirectional support. Has a two-phase read: first attempt, then retry with `TryHarder = true` and `AutoRotate = true` for difficult images. Respects `Color` and `BackgroundColor` from input.

### Spire

Uses GDI+ (Windows only). All formats. Background colour is always white regardless of input. Format detection on read returns `null` for the detected format field.

### QRCoder

Write-only. No `IBarcodeReader`. Uses GDI+ internally.

### UziGranot

Self-contained — no NuGet dependency. Builds a PNG (zlib/deflate) as an intermediate stream, but the returned `IImageFile` is re-encoded as **JPEG**, like the other backends. Can detect multiple QR codes in a single image. Error correction level fixed at M.

## Implementation comparison

| Feature | ZXing | Spire | QRCoder | UziGranot |
|---------|-------|-------|---------|-----------|
| **All 13 formats** | ✓ | ✓ | — | — |
| **Read support** | ✓ | ✓ | — | ✓ |
| **Cross-platform** | ✓ | — (GDI+) | — (GDI+) | — (GDI+) |
| **Custom colours** | ✓ | Foreground only | — | — |
| **TryHarder fallback** | ✓ | — | — | — |
| **Multi-code in image** | — | ✓ | — | ✓ |
| **External dependency** | ZXing.Net | FreeSpire | QRCoder | None |

## Overview

1. **[Index](README.md)** — Overview, interfaces, models, and implementation notes
1. [Examples](examples.md) — QR code, barcode generation, scanning, and layer composition
