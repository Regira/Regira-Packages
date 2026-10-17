# Regira.Office.PDF.PdfPig — index card

> PDF operations without a browser, a server or a licence: merge, split, page removal, text extraction and images to
> PDF on PdfPig (fully managed), and page images through PDFtoImage over PDFium. It makes no PDF from HTML. The guide
> lives on the hub `Regira.Office`:
> `get_package(id: "Regira.Office", section: "office.pdf.instructions", heading: "Backend Comparison")`, under
> *PdfPig behaviour*, with the interfaces under *Interfaces*.

- **It needs an `IImageService`** (`Regira.Media.Drawing.Services.Abstractions`), which parses the images it places and
  returns. Add `Regira.Drawing.SkiaSharp` and register
  `services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>()`; without it, resolving the
  PDF service fails.
- **Register** `services.AddSingleton<IPdfService, Regira.Office.PDF.PdfPig.PdfService>()`; the package has no `Add…`
  extension. `IPdfService` (`Regira.Office.PDF.Abstractions`) is `IPdfEditor` (merge, split, `RemovePages`),
  `IPdfImageService` (`ToImages`, `ImagesToPdf`) and `IPdfTextService` (`GetText`, `GetTextPerPage`,
  `RemoveEmptyPages`) in one. Without DI: `new PdfService(new Regira.Drawing.SkiaSharp.Services.ImageService())`.
- **Read every result with `GetBytes()`** (`Regira.IO.Extensions`, in `Regira.Common`), never `.Bytes`: an
  `IMemoryFile` carries bytes or a stream depending on the method, and `.Bytes` of a stream-backed file is `null`,
  which a download serves as an empty body.
- **Pages are numbered from 1**, in `PdfSplitRange` and `RemovePages`. `Merge` of no files, and `RemovePages` of every
  page, return `null`.
- A scanned page holds no text, so `RemoveEmptyPages` removes it. Every using:
  `get_package(id: "Regira.Office", section: "office.namespaces", heading: "PDF")`.
