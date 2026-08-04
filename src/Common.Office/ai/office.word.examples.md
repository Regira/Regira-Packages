# Office.Word — Example: Contract Generation

> Context: A legal SaaS generates client contracts from a Word template, fills in client details and a line-item table, then converts to PDF for signing.

## Generate a contract from a template

```csharp
IWordService word = new Regira.Office.Word.Spire.WordManager();

var templateBytes = await _fileService.GetBytes("templates/contract.docx");

IMemoryFile contract = await word.Create(new WordTemplateInput
{
    Template         = templateBytes!.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object>
    {
        ["ClientName"]    = client.Name,
        ["ClientAddress"] = client.Address,
        ["StartDate"]     = contractData.StartDate.ToString("d"),
        ["EndDate"]       = contractData.EndDate.ToString("d")
    },
    CollectionParameters = new Dictionary<string, ICollection<IDictionary<string, object>>>
    {
        ["ServiceLines"] = contractData.Lines.Select(l => new Dictionary<string, object>
        {
            ["Description"] = l.Description,
            ["Quantity"]    = l.Quantity,
            ["UnitPrice"]   = l.UnitPrice.ToString("C")
        } as IDictionary<string, object>).ToList()
    }
});
```

## Convert to PDF for signing

```csharp
IMemoryFile pdf = await word.Convert(
    new WordTemplateInput { Template = contract },
    FileFormat.Pdf);

await _fileService.Save($"contracts/{client.Id}/contract-{DateTime.Today:yyyyMMdd}.pdf", pdf.Bytes!);
```

## Merge addendum into the main contract

```csharp
var addendum = await _fileService.GetBytes("templates/addendum.docx");

IMemoryFile merged = await word.Merge(
[
    new WordTemplateInput { Template = contract },
    new WordTemplateInput { Template = addendum!.ToMemoryFile() }
]);
```
