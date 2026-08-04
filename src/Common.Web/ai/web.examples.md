# Web — Example: Invoice Email with Razor Template

> Context: A billing service renders an HTML invoice email using a Razor template, then sends it. The API uses global exception handling and a central route prefix.

## DI Registration

```csharp
// Program.cs
services.AddSingleton<IHtmlParser, Regira.Web.HTML.RazorLight.RazorTemplateParser>(
    _ => new(new() { TemplateKey = "invoice-email" }));

services.AddControllers(options =>
{
    options.UseCentralRoutePrefix(new RouteAttribute("api/v1"));
    options.InputFormatters.Insert(0, new TextPlainInputFormatter());
});

services.AddGlobalExceptionHandling();
app.UseGlobalExceptionHandling();
app.UseRequestCulture();
```

## Render an invoice email body

```csharp
// invoice-email.cshtml (Razor template)
// <h2>Invoice #@Model.Number</h2>
// <p>Dear @Model.CustomerName, ...</p>

public async Task<string> RenderInvoiceEmail(Invoice invoice)
    => await _htmlParser.Parse(await File.ReadAllTextAsync("Templates/invoice-email.cshtml"), invoice);
```

## Simple token-based template (no Razor)

```csharp
var parser = new HtmlTemplateParser(_jsonSerializer);
string html = await parser.Parse(
    "<p>Hello {{Name}}, your order <strong>{{OrderNumber}}</strong> is confirmed.</p>",
    new { Name = "Alice", OrderNumber = "ORD-0042" });
```

## Background export task (System.Hosting)

```csharp
[HttpPost("reports/generate")]
public IActionResult StartReport([FromServices] IBackgroundTaskQueue queue)
{
    queue.QueueBackgroundWorkItem(async token =>
    {
        var report = await _reportService.Generate(token);
        await _fileService.Save("reports/latest.xlsx", report.Bytes!);
    });
    return Accepted();
}
```

## Return a file from a controller

```csharp
[HttpGet("invoices/{id}/download")]
public async Task<IActionResult> Download(int id)
{
    var pdf = await _invoiceService.GetPdf(id);
    return this.File(pdf, inline: false);   // ControllerExtensions
}
```
