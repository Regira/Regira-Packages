# Office.Barcodes.Testing

NUnit tests for the [barcode and QR code backends](../../src/Common.Office/docs/barcodes/README.md) of Regira Office:
[Barcodes.QRCoder](../../src/Barcodes.QRCoder/README.md), [Barcodes.Spire](../../src/Barcodes.Spire/README.md),
[Barcodes.UziGranot](../../src/Barcodes.UziGranot/README.md) and [Barcodes.ZXing](../../src/Barcodes.ZXing/README.md).
Each backend runs a shared write-and-read-back suite; tests a backend cannot do (reading with the write-only QRCoder,
features missing from Spire's free edition) are ignored.

## Running

```bash
dotnet test tests/Office.Barcodes.Testing
```

The QRCoder, Spire and Uzi Granot backends render through [Drawing.GDI](../../src/Drawing.GDI/README.md)
(System.Drawing), so their fixtures need Windows; the ZXing fixtures use SkiaSharp. Source images are in
`Assets/Input`; generated codes are written under `Assets/Output` (git-ignored).
