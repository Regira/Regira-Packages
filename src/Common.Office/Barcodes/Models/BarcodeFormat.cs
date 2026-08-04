namespace Regira.Office.Barcodes.Models;

[Flags]
public enum BarcodeFormat
{
    UnKnown = 0,
    Code39 = 1 << 0,
    Code93 = 1 << 1,
    Code128 = 1 << 2,
    CodaBar = 1 << 3,
    DataMatrix = 1 << 4,
    Ean13 = 1 << 5,
    Ean8 = 1 << 6,
    Itf = 1 << 7,
    QRCode = 1 << 8,
    Upca = 1 << 9,
    Upce = 1 << 10,
    Pdf417 = 1 << 11,
    Aztec = 1 << 12,
    Any = Code39 | Code93 | Code128 | CodaBar | DataMatrix | Ean13 | Ean8 | Itf | QRCode | Upca | Upce | Pdf417 | Aztec
}