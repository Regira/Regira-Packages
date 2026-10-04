# Regira.Office.Barcodes.ZXing

Barcode backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [ZXing.Net](https://www.nuget.org/packages/ZXing.Net.Bindings.SkiaSharp) with SkiaSharp rendering. `BarcodeService` implements `IBarcodeService` and `QRCodeService` implements `IQRCodeService`, reading and writing all 13 formats. It is the only cross-platform barcode backend and the recommended one; a read that finds nothing retries with `TryHarder` and `AutoRotate`.

## Installation

```xml
<PackageReference Include="Regira.Office.Barcodes.ZXing" Version="6.*" />
```

It renders through SkiaSharp (`Regira.Drawing.SkiaSharp`), whose package brings the native library for Windows and macOS only. On Linux, add `SkiaSharp.NativeAssets.Linux` at the version of `SkiaSharp` the application resolves.

## Documentation

- [Barcodes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/barcodes/) — the shared barcode and QR contracts, the input models, how the backends compare, and why `format: null`, not `BarcodeFormat.Any`, scans every symbology
- [Barcodes examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/barcodes/examples.html) — generating QR codes and barcodes, scanning, and composing a code into an image

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
