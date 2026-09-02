# Regira Office — Namespace Reference

> **AI Agent Rule**: You MUST use the exact namespaces listed in this file.
> You are NOT allowed to guess, invent, or assume any namespace.
> If a type is not listed here, look it up with `get_type` before using it.

Every Office module splits the same way: **`Regira.Office.<Area>.Abstractions`** holds the interfaces you
inject, **`…Models`** holds the input/option types you construct, and a **provider namespace**
(`Regira.Office.PDF.SelectPdf`, …) holds the one concrete class you register. Inject the abstraction, name
the provider only at registration.

---

## Shared across every module

| Namespace | Types |
|---|---|
| `Regira.IO.Abstractions` | `IMemoryFile`, `IBinaryFile`, `IMemoryBytesFile`, `IMemoryStreamFile` — **every Office service returns `IMemoryFile`**; assembly `Regira.Common`, not an Office package |
| `Regira.IO.Extensions` | `MemoryFileExtensions`, `BinaryFileExtensions` — `GetBytes()`, `GetStream()`, `HasPath()`. Assembly `Regira.Common` |
| `Regira.Office.Models` | `FileFormat`, `PageSize`, `PageSizes`, `PageOrientation`, `Margins` |
| `Regira.Office.MimeTypes` | `ContentTypes` |
| `Regira.Office.Utilities` | `PageSizeUtility` |
| `Regira.Media.Drawing.Models.Abstractions` | `IImageFile` (extends `IMemoryFile`) — what the barcode/QR and image services return; assembly `Regira.Media` |

⚠️ `GetBytes()` is an extension in **`Regira.IO.Extensions`** — a file object with no `using` for it looks
like it has no content accessor at all.

---

## PDF

| Namespace | Types |
|---|---|
| `Regira.Office.PDF.Abstractions` | `IPdfService`, `IHtmlToPdfService`, `IPdfEditor`, `IPdfMerger`, `IPdfSplitter`, `IPdfTextExtractor`, `IPdfTextService`, `IPdfImageService`, `IImagesToPdfService`, `IPdfToImageService`, `IPdfToImageAsyncService`, `IPdfPrinter`, `PdfInputBase` |
| `Regira.Office.PDF.Models` | `HtmlInput`, `ImagesInput`, `PdfSplitRange`, `PdfToImagesOptions` |
| `Regira.Office.PDF.Defaults` | `PdfDefaults` |
| `Regira.Office.PDF.Drawing` | `PdfImageCreator`, `PdfToImageLayerOptions` |
| `Regira.Office.PDF.Printer` | `PdfPrinterInput` |
| **Providers** | `Regira.Office.PDF.SelectPdf` · `…DocNET` · `…Spire` · `…Puppeteer` · `…MsPlaywright` → `PdfManager`; `Regira.Office.PDF.PDFtoPrinter` · `…PockyBum522` → `PdfPrinter` |

## Barcodes & QR

| Namespace | Types |
|---|---|
| `Regira.Office.Barcodes.Abstractions` | `IBarcodeService`, `IBarcodeReader`, `IBarcodeWriter`, `IQRCodeService`, `IQRCodeReader`, `IQRCodeWriter`, `BarcodeInputBase` |
| `Regira.Office.Barcodes.Models` | `BarcodeInput`, `QRCodeInput`, `BarcodeFormat`, `BarcodeReadResult` |
| `Regira.Office.Barcodes.Defaults` | `BarcodeDefaults` |
| `Regira.Office.Barcodes.Drawing` | `BarcodeImageCreator` |
| **Providers** | `Regira.Office.Barcodes.ZXing` · `…Spire` → `BarcodeService`, `QRCodeService`; `Regira.Office.Barcodes.QRCoder` → `QRCodeWriter`; `Regira.Office.Barcodes.UziGranot` → `QRCodeService` |

⚠️ **`BarcodeFormat` is ambiguous.** `Regira.Office.Barcodes.Models.BarcodeFormat` and ZXing's own
`BarcodeFormat` collide the moment both `using`s are present. Alias it:
`using BarcodeFormat = Regira.Office.Barcodes.Models.BarcodeFormat;`

`QRCodeInput` has an implicit conversion from `string`, so `qr.Create("https://…")` and
`qr.Create(new QRCodeInput { Content = "…" })` are both valid — the object form is only needed when you set
size or error correction.

## Excel

| Namespace | Types |
|---|---|
| `Regira.Office.Excel.Abstractions` | `IExcelService`, `IExcelServiceCore`, `IExcelReader`, `IExcelWriter` |
| `Regira.Office.Excel.Models` | `ExcelSheet` |
| **Providers** | `Regira.Office.Excel.MiniExcel` · `…ClosedXML` · `…EPPlus` · `…NpoiMapper` → `ExcelManager`, `Options` |

## Word

| Namespace | Types |
|---|---|
| `Regira.Office.Word.Abstractions` | `IWordService`, `IWordManager`, `IWordCreator`, `IWordConverter`, `IWordMerger`, `IWordTextExtractor`, `IWordImageExtractor`, `IWordToImagesService` |
| `Regira.Office.Word.Models` | `WordTemplateInput`, `WordHeaderFooterInput`, `WordImage`, `WordTable`, `Paragraph`, `ParagraphStyle`, `DocumentSettings`, `ConversionOptions`, `InputOptions`, `HeaderFooterType`, `HorizontalAlignment` |
| `Regira.Office.Word.Drawing` | `WordImageCreator`, `WordToImageLayerOptions` |
| **Providers** | `Regira.Office.Word.Spire` → `WordManager`, `DocumentBuilder`, `WordDocumentSettings`; `Regira.Office.Word.Mini` → `WordCreator` |

## Mail

| Namespace | Types |
|---|---|
| `Regira.Office.Mail.Abstractions` | `IMailService`, `IMailAddress`, `IMailRecipient`, `IMailResponse`, `IMessageObject`, `IMessageParser`, `MailerBase` |
| `Regira.Office.Mail.Models` | `MailAddress`, `MailRecipient`, `MailResponse`, `MessageObject`, `RecipientTypes` |
| `Regira.Office.Mail.Extensions` | `MessageObjectExtensions` |
| `Regira.Office.Mail.Exceptions` | `MailException`, `EmailFormatException` |
| `Regira.Office.Mail.Web` | `MailInput`, `Address`, `Recipient`, `Attachment` — DTOs for an HTTP endpoint |
| `Regira.Office.Mail.Services` | `DummyMailer` |
| **Providers** | `Regira.Office.Mail.SendGrid` → `SendGridMailer`, `SendGridConfig`; `Regira.Office.Mail.MailGun` → `MailGunMailer`, `MailgunConfig`; `Regira.Office.Mail.MSGReader` → `MsgParser`, `EmlParser` |
| **DI** | `Regira.Office.Mail.SendGrid.DependencyInjection` · `Regira.Office.Mail.MailGun.DependencyInjection` → `ServiceCollectionExtensions` |

## CSV · OCR · vCards · Printing

| Namespace | Types |
|---|---|
| `Regira.Office.Csv.Abstractions` | `ICsvService` |
| `Regira.Office.Csv.Models` | `CsvOptions` |
| `Regira.Office.Csv.CsvHelper` | `CsvManager`, `CsvHelperOptions` |
| `Regira.Office.OCR.Abstractions` | `IOcrService` |
| `Regira.Office.OCR.Models.DTO` | `OcrResult` |
| `Regira.Office.OCR.Tesseract` · `…PaddleOCR` | `OcrManager`, `Options` |
| `Regira.Office.VCards.Abstractions` | `IVCardService`, `VCardVersion`, `VCardPropertyType`, `VCardTelType`, `VCardGenderSex` |
| `Regira.Office.VCards.Models` | `VCard`, `VCardName`, `VCardAddress`, `VCardEmail`, `VCardTel`, `VCardOrganization`, `VCardBirthdate`, `VCardGender`, `VPhoto` |
| `Regira.Office.VCards.FolkerKinzel` | `VCardManager` |
| `Regira.Office.Printing.Abstractions` | `IPrintService` |
| `Regira.Office.Printing.Models` | `ImagePrintInputModel`, `Duplex`, `PaperSourceKind` |

## Remote clients (`Regira.Office.Clients`)

Call another host's Office API instead of running the engine in-process.

| Namespace | Types |
|---|---|
| `Regira.Office.Clients.Services` | `PdfClient`, `WordClient`, `ExcelClient`, `CsvClient`, `BarcodeClient`, `QRCodeClient`, `OcrClient`, `MessageParserClient`, `LicenseStatusClient` |
| `Regira.Office.Clients.Abstractions` | `OfficeClientBase`, `ILicenseStatusClient` |
| `Regira.Office.Clients.DependencyInjection` | `OfficeClientServiceCollectionExtensions`, `OfficeClientOptions` |

---

## Grouped by use case (quick lookup)

### HTML → PDF

```
Regira.Office.PDF.Abstractions   → IHtmlToPdfService
Regira.Office.PDF.Models         → HtmlInput
Regira.Office.Models             → PageSize, PageOrientation, Margins
Regira.IO.Abstractions           → IMemoryFile          (the return type)
Regira.IO.Extensions             → GetBytes(), GetStream()
Regira.Office.PDF.SelectPdf      → PdfManager           (registration only)
```

### QR code / barcode image

```
Regira.Office.Barcodes.Abstractions            → IQRCodeService, IBarcodeService
Regira.Office.Barcodes.Models                  → QRCodeInput, BarcodeInput, BarcodeFormat
Regira.Media.Drawing.Models.Abstractions       → IImageFile          (the return type)
Regira.Office.Barcodes.ZXing                   → QRCodeService       (registration only)
```

### Merge / split / read an existing PDF

```
Regira.Office.PDF.Abstractions   → IPdfMerger, IPdfSplitter, IPdfTextExtractor, IPdfEditor
Regira.Office.PDF.Models         → PdfSplitRange
Regira.IO.Abstractions           → IMemoryFile
Regira.Office.PDF.DocNET         → PdfManager           (registration only)
```

### Generate a Word document from a template

```
Regira.Office.Word.Abstractions  → IWordService, IWordManager
Regira.Office.Word.Models        → WordTemplateInput, WordImage, WordTable, DocumentSettings
Regira.Office.Word.Spire         → WordManager          (registration only)
```

### Send mail

```
Regira.Office.Mail.Abstractions  → IMailService
Regira.Office.Mail.Models        → MessageObject, MailAddress, MailRecipient, RecipientTypes
Regira.Office.Mail.SendGrid      → SendGridMailer, SendGridConfig
Regira.Office.Mail.SendGrid.DependencyInjection → ServiceCollectionExtensions
```

### Read or write a spreadsheet

```
Regira.Office.Excel.Abstractions → IExcelService, IExcelReader, IExcelWriter
Regira.Office.Excel.Models       → ExcelSheet
Regira.Office.Excel.MiniExcel    → ExcelManager         (registration only)
```
