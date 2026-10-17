# Regira Office.Word — Examples
<!-- {% raw %} -->

## Example 1: Fill a template with scalar parameters

Replace `{{CustomerName}}`, `{{InvoiceDate}}`, and other placeholders in a .docx template.

<!-- no-compile -->
```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();

byte[] templateBytes = await File.ReadAllBytesAsync("Templates/Invoice.docx");

IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template         = templateBytes.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object>
    {
        ["CustomerName"] = "Alice Corp",
        ["InvoiceDate"]  = DateTime.Today.ToString("d"),
        ["InvoiceNo"]    = "INV-2024-0042",
        ["Total"]        = "€ 1 250,00"
    }
});

await fileService.Save("invoices/INV-2024-0042.docx", doc.GetBytes()!);
```

---

## Example 2: Fill a table (collection parameter)

The template holds a table whose Alt Text title is `Items`: a header row, then a template row whose cells hold
`{{Description}}`, `{{Qty}}`, `{{UnitPrice}}` and `{{LineTotal}}`. The template row is written once per order line,
and each dictionary fills one copy of it (see [Collection tables](README.md#collection-tables)).

<!-- no-compile -->
```csharp
IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template             = templateBytes.ToMemoryFile(),
    GlobalParameters     = new Dictionary<string, object> { ["OrderNo"] = "ORD-001" },
    CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
    {
        ["Items"] = orderLines.Select(l => (IDictionary<string, object>)new Dictionary<string, object>
        {
            ["Description"] = l.ProductName,
            ["Qty"]         = l.Quantity,
            ["UnitPrice"]   = l.UnitPrice.ToString("C"),
            ["LineTotal"]   = l.LineTotal.ToString("C")
        }).ToList()
    }
});
```

---

## Example 3: Replace an image placeholder

<!-- no-compile -->
```csharp
byte[] logoBytes = await File.ReadAllBytesAsync("assets/logo.png");

IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template = templateBytes.ToMemoryFile(),
    Images   =
    [
        new WordImage
        {
            Name = "CompanyLogo",
            File = logoBytes.ToMemoryFile("image/png"),
            Size = new ImageSize(200, 60)
        }
    ]
});
```

---

## Example 4: Convert DOCX to PDF

<!-- no-compile -->
```csharp
IMemoryFile pdf = await word.Convert(
    new WordTemplateInput { Template = docxFile },
    new ConversionOptions
    {
        OutputFormat      = FileFormat.Pdf,
        AutoScaleTables   = true,
        AutoScalePictures = true,
        Settings          = new DocumentSettings { PageSize = PageSize.A4 }
    });
```

> ⚠️ `word` is Word.Spire here, and the FreeSpire.Doc free edition writes only the first three pages of a PDF, with a
> notice page in place of the rest, and no error. For a document that can run longer, run Word.Spire on the
> [commercial Spire.Doc](README.md#commercial-spiredoc), or convert with Word.Syncfusion, Word.Aspose or Word.Gotenberg.

---

## Example 5: Merge multiple documents

<!-- no-compile -->
```csharp
var inputs = reportSections.Select(s => new WordTemplateInput
{
    Template         = s.TemplateBytes.ToMemoryFile(),
    GlobalParameters = s.Parameters
}).ToArray();

IMemoryFile merged = await word.Merge(inputs);
```

---

## Example 6: Extract text for search indexing

<!-- no-compile -->
```csharp
string text = await word.GetText(new WordTemplateInput { Template = docxFile });
await searchIndex.AddDocumentAsync(documentId, text);
```

---

## Example 7: Convert each page to an image

<!-- no-compile -->
```csharp
var images = (await word.ToImages(new WordTemplateInput { Template = docxFile })).ToList();

for (int i = 0; i < images.Count; i++)
    await fileService.Save($"previews/page-{i + 1}.jpg", images[i].GetBytes()!);
```

---

## Example 8: Fill a template and convert it to PDF without a vendor licence

Word.Mini fills the template and a Gotenberg server lays it out. Register both, with a PDF rasteriser for page previews:

```csharp
using Regira.Media.Drawing.Services.Abstractions;
using Regira.Office.PDF.Abstractions;
using Regira.Office.Word.Gotenberg.DependencyInjection;

services.AddSingleton<IImageService, Regira.Drawing.SkiaSharp.Services.ImageService>();
services.AddSingleton<IPdfToImageService, Regira.Office.PDF.PdfPig.PdfService>();
services.AddSingleton<IWordCreator, Regira.Office.Word.Mini.WordService>();
services.AddGotenbergWord(o => o.BaseUrl = configuration["Gotenberg:BaseUrl"]!);
```

The converter renders the template input through Word.Mini before it uploads it:

<!-- no-compile -->
```csharp
// IWordConverter converter, IWordToImagesService previews — injected
var input = new WordTemplateInput
{
    Template         = templateBytes.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object> { ["CustomerName"] = "Alice" }
};

IMemoryFile pdf = await converter.Convert(input, FileFormat.Pdf);
IEnumerable<IImageFile> pages = await previews.ToImages(input);
```

---

## Example 9: Keep an optional passage only when it applies

The template wraps the discount paragraph and its table in a [conditional block](README.md#conditional-blocks),
each marker in a paragraph of its own:

```text
{{#if Discounts}}
Discounts granted on this order:
[table "Discounts"]
{{else}}
No discounts apply to this order.
{{/if}}
```

`Discounts` is a collection, so the block holds when it has rows:

<!-- no-compile -->
```csharp
IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template             = templateBytes.ToMemoryFile(),
    CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
    {
        ["Discounts"] = order.Discounts.Select(d => (IDictionary<string, object>)new Dictionary<string, object>
        {
            ["Description"] = d.Description,
            ["Amount"]      = d.Amount.ToString("C")
        }).ToList()
    }
});
```

## Example 10: Write a section per order, with its lines

The template [loops](README.md#loop-blocks) over the orders, and over each order's lines in
[marker rows](README.md#marker-rows), each marker alone in its own paragraph or row:

```text
{{#each Orders}}
Order {{Number}} of {{Date}}
| Description     | Price     |
| {{#each Lines}} |           |
| {{Description}} | {{Price}} |
| {{/each}}       |           |
{{else}}
There are no orders this month.
{{/each}}
```

<!-- no-compile -->
```csharp
IMemoryFile doc = await word.Create(new WordTemplateInput
{
    Template             = templateBytes.ToMemoryFile(),
    CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
    {
        ["Orders"] = orders.Select(o => (IDictionary<string, object>)new Dictionary<string, object>
        {
            ["Number"] = o.Number,
            ["Date"]   = o.Date.ToString("d"),
            ["Lines"]  = o.Lines.Select(l => new { l.Description, Price = l.Price.ToString("C") }).ToList()
        }).ToList()
    }
});
```

---

## Overview

1. [Index](README.md) — Overview, interfaces, models, and implementation notes
1. **[Examples](examples.md)** — Template substitution, conversion, merge, and extraction

<!-- {% endraw %} -->
