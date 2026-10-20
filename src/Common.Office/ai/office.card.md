# Regira.Office — index card

> The office **hub**: abstractions and models for mail, barcodes, PDF, Word, Excel, CSV, OCR and vCards; a backend
> package implements each area. One guide per area on this package — `office.instructions` routes to them — and
> `get_package(id: "Regira.Office", section: "office.namespaces")` gives the exact usings per area.

- **Mail** (`office.mail.instructions`): `AddMailKit` (SMTP), `AddSendGrid` or `AddMailGun` registers
  `IMailService`. A refused send throws `MailException`: log its `ResponseContent`, where the provider says why. Over
  SMTP one refused recipient aborts the whole message, and a connection fault (no server, TLS, a missing STARTTLS or
  AUTH, a timeout) passes through unwrapped.
- **Barcodes and QR codes** (`office.barcodes.instructions`): ZXing (`Regira.Office.Barcodes.ZXing`) reads and writes
  every format on every OS; QRCoder, Spire and UziGranot are Windows only. Output is always a JPEG, with the quiet
  zone a reader needs inside `Size`. Alias `BarcodeFormat` next to ZXing's own.
- **HTML to PDF** (`office.pdf.instructions` → *Backend Comparison*): Playwright or Puppeteer (a browser installed on
  first use), Gotenberg (a server, nothing downloaded) or SelectPdf (Windows, five pages on its free edition).
  `HtmlInput.Margins` is in units of `DPI`, not millimetres: convert with `DimensionsUtility.MmToPt`.
- **PDF operations**: PdfPig merges, splits, renders pages and extracts text; MiniPdf converts docx, xlsx and pptx in
  process; Spire's free edition throws on a PDF past ten pages.
- **Word** (`office.word.instructions`): Word.Spire by default (free caps: 500 paragraphs, 25 tables, three PDF
  pages); Word.Mini fills templates in process; Word.Gotenberg converts to PDF and needs an `IWordCreator` for a
  template with parameters. No Word at hand? `office.word.examples` → *No Word at hand* builds a template in code.
- **Excel** (`office.excel.instructions`): MiniExcel by default; only NpoiMapper reads legacy `.xls`; EPPlus is
  deprecated. Rows land in `ExcelSheet.Data`, nothing streams.
- **CSV** (`office.csv.instructions`): `Regira.Office.Csv.CsvHelper`, typed (`ICsvService<T>`) or as dictionaries.
- **OCR** (`office.ocr.instructions`): Tesseract (English and the Windows natives bundled; Linux and macOS
  need the Tesseract 5 natives installed), PaddleOCR on Windows (12 scripts).
- **vCards** (`office.vcards.instructions`): `VCardManager` from `Regira.Office.VCards.FolkerKinzel`, constructed
  directly.
- **Every result is an `IMemoryFile`: read it with `GetBytes()`** (`Regira.IO.Extensions`), never `.Bytes`, which is
  `null` for every method that returns a stream.
