# Regira Barcodes — Examples

## Example 1: Generate a QR code and save to storage

Create a QR code from a URL and push it to any `IFileService` backend.

```csharp
public async Task<string> CreateQrCode(IQRCodeService qr, IFileService storage, string content, string folder)
{
    using var img = await qr.Create(new QRCodeInput
    {
        Content = content,
        Size    = new ImageSize(400, 400)
    });

    var path = FileNameUtility.Combine(folder, $"{Guid.NewGuid()}.jpg");
    return await storage.Save(path, img.GetBytes()!);
}
```

---

## Example 2: Generate a Code128 barcode for a shipping label

```csharp
public async Task<IImageFile> CreateShippingBarcode(IBarcodeService barcodes, string trackingNumber)
{
    return await barcodes.Create(new BarcodeInput
    {
        Content         = trackingNumber,
        Format          = BarcodeFormat.Code128,
        Size            = new ImageSize(600, 150),
        Color           = "#000000",
        BackgroundColor = "#FFFFFF"
    });
}
```

---

## Example 3: Scan / read a barcode from an uploaded image

```csharp
[HttpPost("scan")]
public async Task<IActionResult> Scan([FromForm] IFormFile file, IBarcodeService barcodes)
{
    using var img = file.ToNamedFile().ToImageFile();

    var result = await barcodes.Read(img);

    if (result?.Contents?.Length > 0)
        return Ok(new { result.Format, Value = result.Contents[0] });

    return NotFound("No barcode detected");
}
```

Force scanning for a specific format to speed up detection:

```csharp
var result = await barcodes.Read(img, BarcodeFormat.Ean13);
```

---

## Example 4: Compose a barcode as an image layer

`BarcodeImageCreator` lets you embed a barcode directly into an `ImageBuilder` composition (see [Drawing docs](../../../Common.Media/README.md)).

```csharp
// Register
services.AddSingleton<IBarcodeWriter>(
    new Regira.Office.Barcodes.ZXing.BarcodeService());
services.AddSingleton<IImageCreator>(provider =>
    new BarcodeImageCreator(provider.GetRequiredService<IBarcodeWriter>()));

// Use in ImageBuilder
var barcodeLayer = new ImageLayer<BarcodeInput>
{
    Source  = new BarcodeInput { Content = "SKU-00042", Format = BarcodeFormat.Code128 },
    Options = new ImageLayerOptions
    {
        Position = ImagePosition.Right | ImagePosition.Bottom,
        Margin   = 10,
        Size     = new ImageSize(300, 80)
    }
};

var result = await new ImageBuilder(imageService, imageCreators)
    .SetBaseLayer(productPhoto)
    .Add(barcodeLayer)
    .Build();
```

---

## Example 5: Backend swap — pick implementation from config

```csharp
IBarcodeService barcodes = configuration["Barcodes:Backend"] switch
{
    "spire" => new Regira.Office.Barcodes.Spire.BarcodeService(),
    _       => new Regira.Office.Barcodes.ZXing.BarcodeService()
};

services.AddSingleton<IBarcodeService>(barcodes);
services.AddSingleton<IQRCodeService>(barcodes is IQRCodeService qr
    ? qr
    : new Regira.Office.Barcodes.ZXing.QRCodeService());
```

---

## Overview

1. [Index](README.md) — Overview, interfaces, models, and implementation notes
1. **[Examples](examples.md)** — QR code, barcode generation, scanning, and layer composition
