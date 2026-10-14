# Office.PDF — Example: Invoice Generation and Archiving

> Context: A billing service converts HTML invoices to PDF, merges monthly invoices into one archive PDF, and extracts text for full-text search indexing.

## HTML → PDF (Playwright)

<!-- no-compile -->
```csharp
IHtmlToPdfService pdf = new Regira.Office.PDF.MsPlaywright.PdfManager();

IMemoryFile invoicePdf = await pdf.Create(new HtmlInput
{
    HtmlContent       = await _htmlParser.Parse(invoiceTemplate, invoice),
    // Chromium fills the pageNumber and totalPages classes
    FooterHtmlContent = "<p style='text-align:center;font-size:9pt'>Page <span class='pageNumber'></span> of <span class='totalPages'></span></p>",
    FooterHeight      = 15,
    Format            = PageSize.A4,
    Margins           = new Margins { Top = 20, Bottom = 20, Left = 15, Right = 15 }
});

await _fileService.Save($"invoices/{invoice.Number}.pdf", invoicePdf.GetBytes()!);
```

## Merge monthly invoices into one PDF

<!-- no-compile -->
```csharp
public async Task<IMemoryFile> MergeMonthlyInvoices(int year, int month)
{
    var identifiers = await _fileService.List(new FileSearchObject
    {
        FolderUri = $"invoices/{year}/{month:D2}/",
        Extensions = [".pdf"]
    });

    var pdfs = new List<IMemoryFile>();
    foreach (var id in identifiers)
    {
        var bytes = await _fileService.GetBytes(id);
        if (bytes != null) pdfs.Add(bytes.ToBinaryFile());
    }

    IPdfMerger merger = new Regira.Office.PDF.PdfPig.PdfService(_imageService);
    return (await merger.Merge(pdfs))!;
}
```

## Extract text for search indexing

<!-- no-compile -->
```csharp
public async Task IndexInvoice(string identifier)
{
    var bytes = await _fileService.GetBytes(identifier) ?? [];
    IPdfTextExtractor extractor = new Regira.Office.PDF.PdfPig.PdfService(_imageService);
    string text = await extractor.GetText(bytes.ToBinaryFile());
    await _searchIndex.Index(identifier, text);
}
```

## Preview the first page

`ToImages` renders every page it is given, so split the first page off before rendering it:

```csharp
IImageService imageService = new Regira.Drawing.SkiaSharp.Services.ImageService();
var pdfService = new Regira.Office.PDF.PdfPig.PdfService(imageService);
IMemoryFile invoicePdf = File.ReadAllBytes("invoice.pdf").ToMemoryFile();

IMemoryFile firstPage = (await pdfService.Split(invoicePdf, [new PdfSplitRange { Start = 1, End = 1 }])).Single();
var preview = (await pdfService.ToImages(firstPage)).First();   // an IImageFile, JPEG by default
```

## Sample PDFs without a browser

Test or seed data needs no Chromium: PdfPig's `PdfDocumentBuilder` (`UglyToad.PdfPig.Writer`, which arrives with
`Regira.Office.PDF.PdfPig`) writes text, MiniPdf converts a `.docx` or `.xlsx`, and `ImagesToPdf` puts an image on a
page of its own.

```csharp
using UglyToad.PdfPig.Core;
using UglyToad.PdfPig.Fonts.Standard14Fonts;
using UglyToad.PdfPig.Writer;

// text
using var builder = new PdfDocumentBuilder();
var font = builder.AddStandard14Font(Standard14Font.Helvetica);
builder.AddPage(UglyToad.PdfPig.Content.PageSize.A4).AddText("Invoice 2026-001", 12, new PdfPoint(50, 780), font);
IMemoryFile textPdf = builder.Build().ToMemoryFile();

// a Word document or a spreadsheet
IDocumentToPdfService converter = new Regira.Office.PDF.MiniPdf.PdfService();
IMemoryFile documentPdf = await converter.Create(new DocumentInput { Document = File.ReadAllBytes("sample.docx").ToMemoryFile() });

// images, a page each
IImagesToPdfService imagesToPdf = new Regira.Office.PDF.PdfPig.PdfService(new Regira.Drawing.SkiaSharp.Services.ImageService());
IMemoryFile scanPdf = (await imagesToPdf.ImagesToPdf(new ImagesInput { Images = [File.ReadAllBytes("scan.png")] }))!;
```
