# Regira.Office.Barcodes.Spire

Barcode backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [FreeSpire.Barcode](https://www.nuget.org/packages/FreeSpire.Barcode). `BarcodeService` implements `IBarcodeService`, and `QRCodeService` implements `IQRCodeService`. Of the two backends that read and write all 13 formats it is the Windows-only one; a read can return several codes from one image but reports no format, and codes are always drawn on a white background.

## Installation

```xml
<PackageReference Include="Regira.Office.Barcodes.Spire" Version="6.*" />
```

Windows only — it renders through GDI+ (`Regira.Drawing.GDI`).

## Documentation

- [Barcodes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/barcodes/) — the shared barcode and QR contracts, the input models, and how the backends compare
- [Barcodes examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/barcodes/examples.html) — generating QR codes and barcodes, scanning, and composing a code into an image

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

The wrapped FreeSpire.Barcode library is licensed separately by its vendor; see its [package page](https://www.nuget.org/packages/FreeSpire.Barcode).
