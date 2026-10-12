# Regira Web — Examples
<!-- {% raw %} -->

## Example 1: Render a Razor invoice template

The parser compiles `Invoice.cshtml` on the first request and renders the cached template on every request after it.

<!-- no-compile -->
```csharp
public class InvoiceService(IHtmlParser html, IHtmlToPdfService pdf)
{
    public async Task<IMemoryFile> GeneratePdf(InvoiceDto invoice)
    {
        string template = await File.ReadAllTextAsync("Templates/Invoice.cshtml");
        string rendered = await html.Parse(template, invoice);

        return await pdf.Create(new HtmlInput { HtmlContent = rendered });
    }
}

// Registration
services.AddSingleton<IHtmlParser>(new Regira.Web.HTML.RazorLight.RazorTemplateParser());
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

For templates without Razor syntax — use `HtmlTemplateParser` and `{token}` placeholders.

<!-- no-compile -->
```csharp
var parser   = new HtmlTemplateParser(jsonSerializer) { HtmlEncode = true };
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
<p>Hi {name},</p>
<p>Your account at <strong>{companyName}</strong> is ready.</p>
<a href="{loginUrl}">Log in now</a>
```

The keys are the JSON serializer's camel-cased property names. `HtmlEncode` encodes each value, so a name holding `<` stays text.

---

## Example 3: Exceptions and culture with ASP.NET Core's middleware

Log unhandled exceptions without exposing internals to the API consumer, and read a `?culture=` parameter on every
request.

```csharp
// Program.cs
services.AddProblemDetails();

app.UseExceptionHandler();
app.UseRequestLocalization(options => options
    .SetDefaultCulture("en")
    .AddSupportedCultures("en", "fr-BE")
    .AddSupportedUICultures("en", "fr-BE"));

// GET /api/dates?culture=fr-BE  → CultureInfo is fr-BE for this request
```

An uncaught exception is logged and answered with a 500 problem response.

---

## Example 4: Background export queue

Queue a long report generation task and return an accepted status immediately. The queue itself ships in
`Regira.System.Hosting` — its API reference is in
[System](https://regira.github.io/Regira-Packages/src/Common.System/docs/hosting.html).

<!-- no-compile -->
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

## Example 5: Render an invoice component

The invoice of Example 1 as a Razor component. It compiles with the application, so a misspelt property fails the build, and the first request renders without compiling anything.

<!-- no-compile -->
```csharp
public class InvoiceService(IHtmlComponentRenderer html, IHtmlToPdfService pdf)
{
    public async Task<IMemoryFile> GeneratePdf(InvoiceDto invoice)
    {
        string rendered = await html.Render<InvoiceTemplate, InvoiceDto>(invoice);

        return await pdf.Create(new HtmlInput { HtmlContent = rendered });
    }
}

// Registration
services.AddTransient<IHtmlComponentRenderer, Regira.Web.HTML.RazorComponents.RazorComponentRenderer>();
```

`Templates/InvoiceTemplate.razor`:

```razor
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

@code {
    [Parameter, EditorRequired] public InvoiceDto Model { get; set; } = null!;
}
```

The project that holds the template uses the Razor SDK: `Microsoft.NET.Sdk.Web`, or `Microsoft.NET.Sdk.Razor` for a worker or class library.

---

## Overview

1. [Index](../README.md) — Overview, template engines, middleware, and Swagger
1. **[Examples](examples.md)** — HTML templating, exception handling, background tasks, Razor components

<!-- {% endraw %} -->
