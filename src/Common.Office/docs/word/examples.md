# Regira Office.Word — Examples

## Example 1: Fill a template with scalar parameters

Replace `{{CustomerName}}`, `{{InvoiceDate}}`, and other placeholders in a .docx template.

```csharp
IWordService word = new Regira.Office.Word.Spire.WordManager();

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

await fileService.Save("invoices/INV-2024-0042.docx", doc.Bytes!);
```

---

## Example 2: Fill a table (collection parameter)

The template contains a bookmark or placeholder row named `Items`. Each dictionary entry maps a column header to a cell value.

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

---

## Example 5: Merge multiple documents

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

```csharp
string text = await word.GetText(new WordTemplateInput { Template = docxFile });
await searchIndex.AddDocumentAsync(documentId, text);
```

---

## Example 7: Convert each page to an image

```csharp
var images = (await word.ToImages(new WordTemplateInput { Template = docxFile })).ToList();

for (int i = 0; i < images.Count; i++)
    await fileService.Save($"previews/page-{i + 1}.jpg", images[i].Bytes!);
```

---

## Overview

1. [Index](README.md) — Overview, interfaces, models, and implementation notes
1. **[Examples](examples.md)** — Template substitution, conversion, merge, and extraction
