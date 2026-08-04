# Regira Office.Barcodes AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Barcodes` | Barcode and QR code generation and scanning |

> **A QR code *is* a barcode** — specifically the 2D `BarcodeFormat.QRCode`. This module treats QR as a subset of barcodes: `QRCodeInput : BarcodeInput`, and `IQRCodeService` is just a QR-restricted convenience over `IBarcodeService`. Anything QR can also be done through the general barcode services. See [Barcode vs QR — which to use](#barcode-vs-qr--which-to-use).

**Related:**
- [Media / Drawing](../../Common.Media/ai/media.instructions.md) — `IImageFile` is the return type of `Create()`
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `IMemoryFile` for file input/output

---

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

> Add the Regira feed to `NuGet.Config`:
> ```xml
> <add key="Regira" value="https://packages.regira.com/v3/index.json" />
> ```

---

## Backend Comparison

| Package | Backend | Formats | Read | Write | Platform |
|---|---|---|---|---|---|
| `Regira.Office.Barcodes.ZXing` | ZXing.Net (SkiaSharp) | All 13 | ✓ | ✓ | **Cross-platform** |
| `Regira.Office.Barcodes.Spire` | FreeSpire.Barcode (GDI+) | All 13 | ✓ | ✓ | Windows only |
| `Regira.Office.Barcodes.QRCoder` | QRCoder (GDI+) | QR only | — | ✓ | Windows only |
| `Regira.Office.Barcodes.UziGranot` | Embedded CPOL (GDI+) | QR only | ✓ | ✓ | Windows only |

> Only **ZXing** is cross-platform. 
> Spire, QRCoder, and UziGranot render through `Regira.Drawing.GDI` / `System.Drawing` (GDI+), which is **Windows-only**.

**Default recommendation:** Use `ZXing` for general use — all formats, read + write, and the only fully cross-platform backend.

### Implementation classes

Each backend lives in its own namespace. Note that **QRCoder is write-only QR** and ships only a writer (`QRCodeWriter`, not `QRCodeService`):

| Package | Barcode service | QR service |
|---|---|---|
| `…ZXing` | `Regira.Office.Barcodes.ZXing.BarcodeService` | `Regira.Office.Barcodes.ZXing.QRCodeService` |
| `…Spire` | `Regira.Office.Barcodes.Spire.BarcodeService` | `Regira.Office.Barcodes.Spire.QRCodeService` |
| `…UziGranot` | — | `Regira.Office.Barcodes.UziGranot.QRCodeService` |
| `…QRCoder` | — | `Regira.Office.Barcodes.QRCoder.QRCodeWriter` *(write-only — `IQRCodeWriter`, no `Read`)* |

---

## Interfaces

### `IBarcodeWriter`

```csharp
Task<IImageFile> Create(BarcodeInput input, CancellationToken cancellationToken = default);
```

### `IBarcodeReader`

```csharp
Task<BarcodeReadResult?> Read(IImageFile img, BarcodeFormat? format = null, CancellationToken cancellationToken = default);
```

### `IQRCodeWriter`

```csharp
Task<IImageFile> Create(QRCodeInput input, CancellationToken cancellationToken = default);
```

### `IQRCodeReader`

```csharp
Task<BarcodeReadResult?> Read(IImageFile qrCode, CancellationToken cancellationToken = default);
```

### `IQRCodeService` (extends `IQRCodeWriter` + `IQRCodeReader`)

QR-restricted convenience over the barcode services. Note that `Create` takes a **`QRCodeInput`** (a `string` works via its implicit conversion) and QR `Read` has **no `format` parameter** — it always scans for `BarcodeFormat.QRCode`:

```csharp
Task<IImageFile>          Create(QRCodeInput input, CancellationToken cancellationToken = default);
Task<BarcodeReadResult?>  Read(IImageFile qrCode, CancellationToken cancellationToken = default);
```

### `IBarcodeService` (extends `IBarcodeReader` + `IBarcodeWriter`)

```csharp
Task<IImageFile>          Create(BarcodeInput input, CancellationToken cancellationToken = default);
Task<BarcodeReadResult?>  Read(IImageFile img, BarcodeFormat? format = null, CancellationToken cancellationToken = default);
```

---

## Models

### `BarcodeInput`

| Property | Type | Default | Description |
|---|---|---|---|
| `Content` | `string` | *(required)* | Data to encode |
| `Format` | `BarcodeFormat` | `Code128` | Barcode format (e.g. `BarcodeFormat.Code128`) |
| `Size` | `ImageSize` | `400 × 100` | Output image dimensions |
| `Color` | `Color` | `Black` | Foreground color |
| `BackgroundColor` | `Color` | `White` | Background color |

A `string` converts implicitly to a `BarcodeInput` (`Content`-only), so `await bc.Create("ABC-1234")` works.

### `QRCodeInput`

A subclass of `BarcodeInput` used by the QR services. Its constructor sets `Format = BarcodeFormat.QRCode` and a square `Size`. A `string` converts implicitly, so `await qr.Create("https://…")` works.

### `BarcodeReadResult`

| Property | Type | Description |
|---|---|---|
| `Contents` | `string[]?` | Decoded values (one per barcode found) |
| `Format` | `BarcodeFormat?` | Detected format |

---

## Usage

```csharp
// Generate a QR code
IQRCodeService qr = new Regira.Office.Barcodes.ZXing.QRCodeService();
IImageFile img = await qr.Create("https://example.com");

// Generate a Code128 barcode
IBarcodeService bc = new Regira.Office.Barcodes.ZXing.BarcodeService();
IImageFile barcode = await bc.Create(new BarcodeInput { Content = "ABC-1234" });

// Read / scan a barcode
BarcodeReadResult? result = await bc.Read(barcode);
Console.WriteLine(result?.Contents?[0]);  // "ABC-1234"
```

> **Output format:** all backends encode the result as **JPEG** regardless of the requested format. Save with a `.jpg` extension, or re-encode via `IImageService.ChangeFormat`.

---

## Barcode vs QR — which to use

A QR code is just one barcode format (`BarcodeFormat.QRCode`), so there are two overlapping entry points:

- **`IBarcodeService`** — general purpose. `Create(BarcodeInput)` with any `Format`; `Read(img, format?)` scans any (or a specified) format. Use for 1D barcodes (Code128, EAN, …), DataMatrix, PDF417, **and** QR.
- **`IQRCodeService`** — QR-only convenience. `Create(QRCodeInput)` (string-friendly) and `Read(qrCode)` hard-wired to QR. Use when you only deal with QR and want the smaller surface.

`new BarcodeService().Create(new BarcodeInput { Content = "…", Format = BarcodeFormat.QRCode })` and `new QRCodeService().Create("…")` produce the same QR image.

