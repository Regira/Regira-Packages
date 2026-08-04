# Office.Barcodes — Example: Warehouse Inventory System

> Context: A warehouse app generates QR codes for new stock items and scans barcodes from incoming shipments.

## Generate a QR code for a stock item

```csharp
IQRCodeService qr  = new Regira.Office.Barcodes.ZXing.QRCodeService();
IImageFile     img = await qr.Create($"https://warehouse.example.com/items/{item.Id}");

// Save to storage (output is JPEG)
await _fileService.Save($"items/{item.Id}/qr.jpg", img.Bytes!);
```

## Generate a Code128 barcode for a shipment

```csharp
IBarcodeService bc      = new Regira.Office.Barcodes.ZXing.BarcodeService();
IImageFile      barcode = await bc.Create(new BarcodeInput
{
    Content = shipment.TrackingCode,
    Format  = BarcodeFormat.Code128,
    Size    = new ImageSize(400, 120)
});
```

## Scan a barcode from an uploaded image

```csharp
public async Task<string?> ScanIncomingLabel(byte[] labelImageBytes)
{
    IBarcodeService     bc     = new Regira.Office.Barcodes.ZXing.BarcodeService();
    IImageFile          img    = new ImageFile { Bytes = labelImageBytes };
    BarcodeReadResult?  result = await bc.Read(img);
    return result?.Contents?.FirstOrDefault();
}
```
