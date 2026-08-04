# Regira Office.PDF — Examples

## Example 1: HTML → PDF with header and footer

```csharp
IHtmlToPdfService pdf = new Regira.Office.PDF.SelectPdf.PdfManager();

IMemoryFile result = await pdf.Create(new HtmlInput
{
    HtmlContent         = reportHtml,
    HeaderHtmlContent   = "<div style='text-align:right;font-size:10px'>Confidential</div>",
    HeaderHeight        = 15,
    FooterHtmlContent   = "<div style='text-align:center;font-size:10px'>Page {{page}} of {{pages}}</div>",
    FooterHeight        = 15,
    Format              = PageSize.A4,
    Orientation         = PageOrientation.Portrait,
    Margins             = [20, 20, 20, 20]   // top, right, bottom, left (points)
});

await fileService.Save("reports/output.pdf", result.Bytes!);
```

---

## Example 2: Render invoice template then convert to PDF

Combine `IHtmlParser` (Razor) with `IHtmlToPdfService` for a full template-to-PDF pipeline.

```csharp
public class InvoicePdfService(IHtmlParser html, IHtmlToPdfService pdf)
{
    public async Task<IMemoryFile> Generate(InvoiceDto invoice)
    {
        string template = await File.ReadAllTextAsync("Templates/Invoice.cshtml");
        string rendered = await html.Parse(template, invoice);

        return await pdf.Create(new HtmlInput
        {
            HtmlContent = rendered,
            Format      = PageSize.A4
        });
    }
}
```

---

## Example 3: Merge multiple PDFs

```csharp
IPdfMerger merger = new Regira.Office.PDF.DocNET.PdfManager(imageService);

var pages = new List<IMemoryFile>
{
    coverBytes.ToBinaryFile(),
    chapterOneBytes.ToBinaryFile(),
    appendixBytes.ToBinaryFile()
};

IMemoryFile merged = (await merger.Merge(pages))!;
```

---

## Example 4: Split a PDF into individual pages

```csharp
IPdfSplitter splitter = new Regira.Office.PDF.DocNET.PdfManager(imageService);

int total  = await splitter.GetPageCount(pdf);
var ranges = Enumerable.Range(1, total)
                       .Select(i => new PdfSplitRange { Start = i, End = i });

IEnumerable<IMemoryFile> pages = await splitter.Split(pdf, ranges);
```

---

## Example 5: Convert PDF pages to images

```csharp
IPdfToImageService converter = new Regira.Office.PDF.Spire.PdfManager();

var images = await converter.ToImages(pdf, new PdfToImagesOptions
{
    Size   = new ImageSize(1200, 1697),   // approx A4 at 144 dpi
    Format = ImageFormat.Jpeg
});

int page = 1;
foreach (var img in images)
    await fileService.Save($"pages/page-{page++}.jpg", img.Bytes!);
```

---

## Example 6: Extract text for search indexing

```csharp
IPdfTextService extractor = new Regira.Office.PDF.DocNET.PdfManager(imageService);

string fullText        = await extractor.GetText(pdf);
IList<string> byPage   = await extractor.GetTextPerPage(pdf);

// Remove pages that contain no text (e.g. blank separator pages)
IMemoryFile cleaned = (await extractor.RemoveEmptyPages(pdf))!;
```

---

## Example 7: Print a PDF

```csharp
IPdfPrinter printer = new Regira.Office.PDF.Spire.PdfPrinter();

Console.WriteLine("Available printers:");
foreach (var p in await printer.List())
    Console.WriteLine($"  {p}");

await printer.Print(new PdfPrinterInput
{
    PdfFile         = pdfFile,
    PrinterName     = "HP LaserJet Pro",
    PageSize        = PageSize.A4,
    PageOrientation = PageOrientation.Portrait
});
```

---

## Overview

1. [Index](README.md) — Overview, interfaces, models, and implementation notes
1. **[Examples](examples.md)** — HTML→PDF, merge, split, text extraction, printing
