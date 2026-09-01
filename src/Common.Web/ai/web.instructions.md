# Regira Web AI Agent Instructions

> Razor-based HTML template rendering plus common web utilities, middleware, and Swagger configuration.

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.Web` | `Regira.Web` | Core web utilities, middleware, exception handling |
| `Web.Analytics` | `Regira.Web.Analytics` | Abstract visitor analytics — pluggable capture, enrichment, and storage hooks |
| `Web.HTML.RazorEngineCore` | `Regira.Web.HTML.RazorEngineCore` | Razor templates via RazorEngineCore |
| `Web.HTML.RazorLight` | `Regira.Web.HTML.RazorLight` | Razor templates via RazorLight |
| `Web.Swagger` | `Regira.Web.Swagger` | Swagger/OpenAPI JWT & API Key support |
| `System.Hosting` | `Regira.System.Hosting` | Host config, background tasks, Windows Service |

---

## Installation

```xml
<!-- Core web utilities and middleware -->
<PackageReference Include="Regira.Web" Version="6.*" />

<!-- Visitor analytics -->
<PackageReference Include="Regira.Web.Analytics" Version="6.*" />

<!-- Razor templates (pick one) -->
<PackageReference Include="Regira.Web.HTML.RazorEngineCore" Version="6.*" />
<PackageReference Include="Regira.Web.HTML.RazorLight" Version="6.*" />

<!-- Swagger JWT + API Key support -->
<PackageReference Include="Regira.Web.Swagger" Version="6.*" />

<!-- Host config, background tasks, Windows Service -->
<PackageReference Include="Regira.System.Hosting" Version="6.*" />
```

---

## HTML Template Parsing

### `IHtmlParser`

```csharp
Task<string> Parse<T>(string html, T model);
```

All three implementations share this interface.

---

### `HtmlTemplateParser` — simple `{{token}}` placeholders

Replaces `{{PropertyName}}` tokens with property values. Supports conditional blocks:

```html
<p>Hello {{Name}}!</p>
<!--{{showAddress}}-->
<p>{{Address}}</p>
<!--{{/showAddress}}-->
```

```csharp
var parser = new HtmlTemplateParser(jsonSerializer);
string html = await parser.Parse(template, new { Name = "Alice", Address = "123 Main St", showAddress = true });
```

---

### `RazorEngineCore.RazorTemplateParser` — full Razor syntax

Best for simple templates without layout inheritance. Strips `@model` directives and `Layout` blocks.

```csharp
IHtmlParser parser = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser();
string html = await parser.Parse(razorTemplate, model);
```

---

### `RazorLight.RazorTemplateParser` — Razor with caching

Lighter alternative with memory caching. Use `TemplateKey` to reuse compiled templates across calls.

```csharp
IHtmlParser parser = new Regira.Web.HTML.RazorLight.RazorTemplateParser(new()
{
    TemplateKey = "invoice-template"
});
string html = await parser.Parse(razorTemplate, model);
```

---

## Template Engine Comparison

| Engine | Class | When to use |
|---|---|---|
| `HtmlTemplateParser` | `Regira.Web.HTML` | Simple token replacement, no Razor |
| `RazorEngineCore` | `Regira.Web.HTML.RazorEngineCore` | Full Razor, no layout support |
| `RazorLight` | `Regira.Web.HTML.RazorLight` | Full Razor, repeated parsing (cached) |

---

## Common.Web Middleware

### Global Exception Handling

```csharp
services.AddGlobalExceptionHandling();
app.UseGlobalExceptionHandling();
```

### Request Culture

Sets `CultureInfo.CurrentCulture` from a `culture` query parameter or route value.

```csharp
app.UseRequestCulture();
// GET /api/products?culture=nl-BE  → sets nl-BE culture
```

### Central Route Prefix

Extension on `MvcOptions` from **`Regira.Web`** (`using Regira.Web.Routing;`). It prepends the prefix to
**every** controller: an attribute-routed selector is combined with it (`[Route("products")]` →
`api/v1/products`), and a selector with no route model of its own takes the prefix as its route. It
registers at the head of `opts.Conventions` so it runs before conventions that read the finished route.

```csharp
services.AddControllers(options =>
    options.UseCentralRoutePrefix(new RouteAttribute("api/v1")));
```

### `TextPlainInputFormatter`

Enables `[FromBody] string` binding for `text/plain` requests.

```csharp
services.AddControllers(options =>
    options.InputFormatters.Insert(0, new TextPlainInputFormatter()));
```

### `ControllerExtensions`

```csharp
return this.File(namedFile, inline: true);  // return INamedFile as download or inline
```

### `RequestUtility` — `HttpRequest` extension methods

```csharp
string     url     = Request.CurrentUrl();
Uri        baseUrl = Request.GetBaseUrl();
Uri        abs     = Request.GetAbsoluteUrl("/images/logo.png");
Uri?       ref     = Request.GetReferrer();
IPAddress? ip      = Request.GetIPAddress();
```

---

## Web.Swagger

Add JWT Bearer and/or API Key inputs to the Swagger UI:

```csharp
builder.Services.AddSwaggerGen(o =>
{
    JwtAuthenticationExtensions.AddJwtAuthentication(o);
    // or
    ApiKeyAuthenticationExtensions.AddApiKeyAuthentication(o, parameterName: "X-Api-Key");
});
```

Display enums as strings in Swagger:

```csharp
builder.Services.AddControllers().DisplayEnumAsString();
```

---

## System.Hosting — `WebHostOptions`

Configure via `appsettings.json` under `"Hosting"`:

| Property | Type | Default | Description |
|---|---|---|---|
| `ServiceName` | `string?` | `null` | App / Windows Service display name |
| `LocalPort` | `int?` | `null` | Override listening port |
| `EnableSwagger` | `bool` | `true` | Toggle Swagger UI |
| `EnableCors` | `bool` | `false` | Toggle CORS |
| `EnableHttps` | `bool` | `false` | Toggle HTTPS redirect |
| `RoutePrefix` | `string?` | `null` | API route prefix |

```csharp
builder.Host.UseWebHostOptions();
```

---

## System.Hosting — Background Tasks

Queue and execute long-running work without blocking requests.

```csharp
services.UseBackgroundQueue();

// In a controller
public IActionResult StartExport(IBackgroundTaskQueue queue)
{
    queue.QueueBackgroundWorkItem(async token =>
    {
        await GenerateReport(token);
    });
    return Accepted();
}
```

Typed tasks with progress tracking:

```csharp
services.UseBackgroundQueue<ReportTask>();

var task = taskManager.Execute<string>(async (sp, t) =>
{
    t.SetProgress(0.5);
    return await GenerateReport(sp, t.Id);
});
```

---
