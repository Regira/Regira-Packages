# Office.PDF — Example: Invoice Generation and Archiving

> Context: A billing service converts HTML invoices to PDF, merges monthly invoices into one archive PDF, and extracts text for full-text search indexing.

## HTML → PDF (SelectPdf)

```csharp
IHtmlToPdfService pdf = new Regira.Office.PDF.SelectPdf.PdfManager();

IMemoryFile invoicePdf = await pdf.Create(new HtmlInput
{
    HtmlContent       = await _htmlParser.Parse(invoiceTemplate, invoice),
    FooterHtmlContent = "<p style='text-align:center;font-size:9pt'>Page [page] of [topage]</p>",
    FooterHeight      = 15,
    Format            = PageSize.A4,
    Margins           = new Margins { Top = 20, Bottom = 20, Left = 15, Right = 15 }
});

await _fileService.Save($"invoices/{invoice.Number}.pdf", invoicePdf.Bytes!);
```

## Merge monthly invoices into one PDF

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

    IPdfMerger merger = new Regira.Office.PDF.DocNET.PdfManager(_imageService);
    return (await merger.Merge(pdfs))!;
}
```

## Extract text for search indexing

```csharp
public async Task IndexInvoice(string identifier)
{
    var bytes = await _fileService.GetBytes(identifier) ?? [];
    IPdfTextExtractor extractor = new Regira.Office.PDF.DocNET.PdfManager(_imageService);
    string text = await extractor.GetText(bytes.ToBinaryFile());
    await _searchIndex.Index(identifier, text);
}
```
