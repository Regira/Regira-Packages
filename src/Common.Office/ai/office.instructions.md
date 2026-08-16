# Regira Office AI Agent Instructions

> Regira Office is a collection of document and communication libraries for .NET. All modules share a common set of abstractions from `Common.Office` and are designed to be interchangeable.

**When a user's request targets a specific Office sub-module, load its dedicated instruction file for the exact API.**

> **Exact `using` directives: [`office.namespaces.md`](./office.namespaces.md).** One table per module —
> the abstraction you inject, the models you construct, and the provider namespace you name only at
> registration. Read it instead of resolving Office types one `get_type` call at a time.

---

## Sub-Modules

| Sub-Module | Namespace | Covers | Instructions |
|---|---|---|---|
| **Barcodes** | `Regira.Office.Barcodes` | Barcode and QR code generation and scanning | `./office.barcodes.instructions.md` |
| **CSV** | `Regira.Office.Csv` | CSV reading and writing (typed + dictionary) | `./office.csv.instructions.md` |
| **Excel** | `Regira.Office.Excel` | Excel workbook reading and writing | `./office.excel.instructions.md` |
| **Mail** | `Regira.Office.Mail` | Email sending, mail DTOs for HTTP endpoints, and `.msg` / `.eml` reading | `./office.mail.instructions.md` |
| **OCR** | `Regira.Office.OCR` | Optical character recognition | `./office.ocr.instructions.md` |
| **PDF** | `Regira.Office.PDF` | HTML→PDF, PDF operations, printing | `./office.pdf.instructions.md` |
| **VCards** | `Regira.Office.VCards` | vCard contact file reading and writing | `./office.vcards.instructions.md` |
| **Word** | `Regira.Office.Word` | Word document creation, conversion, merge, extraction | `./office.word.instructions.md` |

---

## When to Load Which File

| User request | Load |
|---|---|
| Generate QR code or barcode, scan/read a barcode | `office.barcodes.instructions.md` |
| Read or write CSV files | `office.csv.instructions.md` |
| Read or write Excel spreadsheets | `office.excel.instructions.md` |
| Send email, configure a mail provider, accept mail requests over HTTP, or read `.msg` / `.eml` files | `office.mail.instructions.md` |
| Extract text from an image or scanned document | `office.ocr.instructions.md` |
| Convert HTML to PDF, merge/split PDFs, print PDFs, extract PDF text | `office.pdf.instructions.md` |
| Read or write vCard (`.vcf`) contact files | `office.vcards.instructions.md` |
| Create Word documents from templates, convert, merge, or extract content | `office.word.instructions.md` |

---

## Related Modules

- [Drawing / Images](../../Common.Media/ai/media.instructions.md) — `IImageService` used by Barcodes, OCR, and PDF sub-modules
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `IFileService` for file input/output across backends

---

## Reading a result

Every sub-module returns its output as an `IMemoryFile`. ⚠️ **Read it with `GetBytes()`** — the extension in
`Regira.IO.Extensions` (assembly `Regira.Common`) — **never `.Bytes` directly.** `IMemoryFile` extends both
`IMemoryBytesFile` (`Bytes`) and `IMemoryStreamFile` (`Stream`), and a producer fills exactly one of them.
Which one is not a property of the backend you chose: it varies **per method**, and one class mixes both —
DocNET's `Split` hands back bytes while its `Merge(IEnumerable<IMemoryFile>)` hands back a stream. So there
is no rule of thumb to apply and nothing to check at the call site; `.Bytes` simply reads `null` for half the
API, saving or returning an empty body with no exception and no log. `GetBytes()` reads whichever half is
populated, and is correct everywhere.

---

🚨 Always load the sub-module instruction file before writing any code. Do not invent API signatures.
