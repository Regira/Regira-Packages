# Office.Word — Example: Contract Generation
<!-- {% raw %} -->

> Context: A legal SaaS generates client contracts from a Word template, fills in client details and a line-item table, then converts to PDF for signing.

## Generate a contract from a template

The template's service table has the Alt Text title `ServiceLines`, and its second row holds `{{Description}}`,
`{{Quantity}}` and `{{UnitPrice}}`: that row is written once per service line (see *Collection Tables* in
`office.word.instructions.md`).

<!-- no-compile -->
```csharp
IWordService word = new Regira.Office.Word.Spire.WordService();

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

## Optional clauses

The template wraps the early-termination clause in a conditional block — each marker in a paragraph of its own —
and the code decides it with a flag:

```text
{{#if HasTerminationClause}}
Either party may terminate this contract with {{NoticeDays}} days' notice.
{{/if}}
```

<!-- no-compile -->
```csharp
IMemoryFile contract = await word.Create(new WordTemplateInput
{
    Template         = templateBytes!.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object>
    {
        ["ClientName"]           = client.Name,
        ["HasTerminationClause"] = contractData.NoticeDays > 0,
        ["NoticeDays"]           = contractData.NoticeDays
    }
});
```

## Convert to PDF for signing

<!-- no-compile -->
```csharp
IMemoryFile pdf = await word.Convert(
    new WordTemplateInput { Template = contract },
    FileFormat.Pdf);

await _fileService.Save($"contracts/{client.Id}/contract-{DateTime.Today:yyyyMMdd}.pdf", pdf.GetBytes()!);
```

> ⚠️ `word` is Word.Spire here, and the FreeSpire.Doc free edition writes only the first three pages of a PDF, with a
> notice page in place of the rest, and no error. For a contract that can run longer, convert with Word.Syncfusion,
> Word.Aspose or Word.Gotenberg (*Without a vendor licence*, below).

### Without a vendor licence

Word.Mini fills the template and a Gotenberg server produces the PDF. The example keeps to scalar
parameters, which every creator fills the same way.

<!-- no-compile -->
```csharp
// Program.cs
builder.Services.AddSingleton<IWordCreator, Regira.Office.Word.Mini.WordService>();
builder.Services.AddGotenbergWord(o => o.BaseUrl = builder.Configuration["Gotenberg:BaseUrl"]!);

// IWordConverter converter — injected; the template input is rendered by Word.Mini before upload
IMemoryFile pdf = await converter.Convert(new WordTemplateInput
{
    Template         = templateBytes!.ToMemoryFile(),
    GlobalParameters = new Dictionary<string, object> { ["ClientName"] = client.Name }
}, FileFormat.Pdf);
```

## Merge addendum into the main contract

<!-- no-compile -->
```csharp
var addendum = await _fileService.GetBytes("templates/addendum.docx");

IMemoryFile merged = await word.Merge(
[
    new WordTemplateInput { Template = contract },
    new WordTemplateInput { Template = addendum!.ToMemoryFile() }
]);
```

<!-- {% endraw %} -->
