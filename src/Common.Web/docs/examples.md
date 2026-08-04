# Regira Web.HTML — Examples

## Example 1: Render a Razor invoice template

Use `RazorLight` with a template key so the compiled template is cached across requests.

```csharp
public class InvoiceService(IHtmlParser html, IHtmlToPdfService pdf)
{
    private const string TemplateKey = "invoice-v1";

    public async Task<IMemoryFile> GeneratePdf(InvoiceDto invoice)
    {
        string template = await File.ReadAllTextAsync("Templates/Invoice.cshtml");
        string rendered = await html.Parse(template, invoice);

        return await pdf.Create(new HtmlInput { HtmlContent = rendered });
    }
}

// Registration
services.AddSingleton<IHtmlParser>(
    new Regira.Web.HTML.RazorLight.RazorTemplateParser(new() { TemplateKey = TemplateKey }));
```

`Templates/Invoice.cshtml`:

```cshtml
<h1>Invoice #@Model.Number</h1>
<p>Customer: @Model.CustomerName</p>
<table>
@foreach (var line in Model.Lines)
{
    <tr>
        <td>@line.Description</td>
        <td>@line.Total.ToString("C")</td>
    </tr>
}
</table>
```

---

## Example 2: Simple token-replacement template

For templates without Razor syntax — use `HtmlTemplateParser` and `{{Token}}` placeholders.

```csharp
var parser   = new HtmlTemplateParser(jsonSerializer);
var template = await File.ReadAllTextAsync("Templates/Welcome.html");

string html = await parser.Parse(template, new
{
    Name        = "Alice",
    CompanyName = "Acme Inc.",
    LoginUrl    = "https://app.example.com/login"
});
```

`Templates/Welcome.html`:

```html
<p>Hi {{Name}},</p>
<p>Your account at <strong>{{CompanyName}}</strong> is ready.</p>
<!--{{LoginUrl}}-->
<a href="{{LoginUrl}}">Log in now</a>
<!--{{/LoginUrl}}-->
```

The `<!--{{LoginUrl}}-->` block is only rendered when `LoginUrl` is truthy.

---

## Example 3: Global exception handling middleware

Add structured error logging without exposing internals to the API consumer.

```csharp
// Program.cs
services.AddGlobalExceptionHandling();
app.UseGlobalExceptionHandling();
```

All uncaught exceptions now return a clean 500 and are logged to `ILogger<GlobalExceptionHandler>`.

---

## Example 4: Culture from query string

Support multi-language APIs by reading a `?culture=` parameter on every request.

```csharp
app.UseRequestCulture();

// GET /api/dates?culture=fr-BE  → CultureInfo is set to fr-BE for this request
```

---

## Example 5: Background export queue

Queue a long report generation task and return an accepted status immediately.

```csharp
// Program.cs
services.UseBackgroundQueue<ReportTask>();

// Controller
[HttpPost("reports/export")]
public IActionResult StartExport(
    [FromBody] ReportRequest req,
    IBackgroundQueueManager<ReportTask> queue)
{
    var task = queue.Execute<string>(async (sp, t) =>
    {
        t.SetProgress(0.0);
        var report = await sp.GetRequiredService<IReportService>()
                              .Generate(req, t.Token);
        t.SetProgress(1.0);
        return report.DownloadUrl;
    });

    return Accepted(new { TaskId = task.Id });
}

// Poll endpoint
[HttpGet("reports/{taskId}/status")]
public IActionResult GetStatus(string taskId, IBackgroundTaskManager<ReportTask> mgr)
{
    var task = mgr.Find(taskId);
    if (task == null) return NotFound();

    return Ok(new
    {
        task.Status,
        task.Progress,
        Result = task.GetResult<string>()
    });
}
```

---

## Overview

1. [Index](../README.md) — Overview, template engines, middleware, Swagger, and hosting
1. **[Examples](examples.md)** — HTML templating, exception handling, background tasks
