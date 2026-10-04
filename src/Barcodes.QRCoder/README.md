# Regira.Office.Barcodes.QRCoder

QR code backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/), built on [QRCoder](https://www.nuget.org/packages/QRCoder). `QRCodeWriter` implements `IQRCodeWriter`. Of the four barcode backends it is the write-only one: it generates QR codes in black on white, and reads nothing.

## Installation

```xml
<PackageReference Include="Regira.Office.Barcodes.QRCoder" Version="6.*" />
```

Windows only — it renders through GDI+ (`Regira.Drawing.GDI`).

## Documentation

- [Barcodes](https://regira.github.io/Regira-Packages/src/Common.Office/docs/barcodes/) — the shared barcode and QR contracts, the input models, and how the backends compare
- [Barcodes examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/barcodes/examples.html) — generating QR codes and barcodes, scanning, and composing a code into an image

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
