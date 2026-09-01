# Regira Web.HTML

Regira Web.HTML provides Razor-based HTML template rendering plus common web utilities, middleware, and Swagger configuration.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.Web` | `Regira.Web` | Core web utilities, middleware, exception handling |
| `Web.Analytics` | `Regira.Web.Analytics` | Abstract visitor analytics — pluggable capture, enrichment, and storage hooks |
| `Web.HTML.RazorEngineCore` | `Regira.Web.HTML.RazorEngineCore` | Razor templates via RazorEngineCore |
| `Web.HTML.RazorLight` | `Regira.Web.HTML.RazorLight` | Razor templates via RazorLight |
| `Web.Swagger` | `Regira.Web.Swagger` | Swagger/OpenAPI JWT & API Key support |
| `System.Hosting` | `Regira.System.Hosting` | Host config, background tasks, Windows Service |

## Installation

```xml
<!-- Core web utilities -->
<PackageReference Include="Regira.Web" Version="6.*" />

<!-- Visitor analytics -->
<PackageReference Include="Regira.Web.Analytics" Version="6.*" />

<!-- Razor templates (pick one) -->
<PackageReference Include="Regira.Web.HTML.RazorEngineCore" Version="6.*" />
<PackageReference Include="Regira.Web.HTML.RazorLight" Version="6.*" />

<!-- Swagger -->
<PackageReference Include="Regira.Web.Swagger" Version="6.*" />

<!-- Hosting utilities -->
<PackageReference Include="Regira.System.Hosting" Version="6.*" />
```

---

## HTML Template Parsing

### IHtmlParser

```csharp no-compile
Task<string> Parse<T>(string html, T model);
```

All three implementations share this interface.

### HtmlTemplateParser (simple placeholder engine)

Replaces `{{PropertyName}}` tokens with property values serialized via `ISerializer`. Supports comment-based conditional blocks:

```html
<p>Hello {{Name}}!</p>
<!--{{showAddress}}-->
<p>{{Address}}</p>
<!--{{/showAddress}}-->
```

```csharp
ISerializer jsonSerializer = new JsonSerializer();
string template = "<p>Hello {{Name}}!</p>";

var parser = new HtmlTemplateParser(jsonSerializer);
string html = await parser.Parse(template, new { Name = "Alice", Address = "123 Main St", showAddress = true });
```

### RazorEngineCore

Full Razor syntax. Strips `@model` directives and `Layout` blocks (not supported by the engine). Best for simple templates without layout inheritance.

```csharp
string razorTemplate = "<p>Hello @Model.Name</p>";
var model = new { Name = "Alice" };

IHtmlParser parser = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser();
string html = await parser.Parse(razorTemplate, model);
```

### RazorLight

Lighter alternative with memory caching. Supports a `TemplateKey` option for cache reuse.

```csharp
string razorTemplate = "<p>Hello @Model.Name</p>";
var model = new { Name = "Alice" };

IHtmlParser parser = new Regira.Web.HTML.RazorLight.RazorTemplateParser(new()
{
    TemplateKey = "invoice-template"   // reuse compiled template across calls
});
string html = await parser.Parse(razorTemplate, model);
```

---

## Common.Web Utilities

### GlobalExceptionHandlingMiddleware

Catches unhandled exceptions and logs them without exposing internals to the caller.

```csharp
var builder = WebApplication.CreateBuilder();
builder.Services.AddGlobalExceptionHandling();

var app = builder.Build();
app.UseGlobalExceptionHandling();
```

### RequestCultureMiddleware

Sets `CultureInfo.CurrentCulture` from a `culture` route value or query parameter.

```csharp
var app = WebApplication.Create();
app.UseRequestCulture();
// Request: GET /api/products?culture=nl-BE  → sets nl-BE culture
```

### RoutePrefixConvention

Apply a central route prefix to every controller.

```csharp
var services = new ServiceCollection();
services.AddControllers(options =>
    options.UseCentralRoutePrefix(new RouteAttribute("api/v1")));
```

### TextPlainInputFormatter

Enables `[FromBody] string` binding for `text/plain` requests.

```csharp
var services = new ServiceCollection();
services.AddControllers(options =>
    options.InputFormatters.Insert(0, new TextPlainInputFormatter()));
```

### ControllerExtensions

```csharp no-compile
// Return INamedFile as a download or inline
return this.File(namedFile, inline: true);
```

### RequestUtility

Extension methods on `HttpRequest`:

```csharp no-compile
string  url     = Request.CurrentUrl();
Uri     baseUrl = Request.GetBaseUrl();
Uri     abs     = Request.GetAbsoluteUrl("/images/logo.png");
Uri?    referrer = Request.GetReferrer();
IPAddress? ip   = Request.GetIPAddress();
```

---

## Web.Swagger

Add JWT Bearer and/or API Key inputs to the Swagger UI:

```csharp
var builder = WebApplication.CreateBuilder();
builder.Services.AddSwaggerGen(o =>
{
    JwtAuthenticationExtensions.AddJwtAuthentication(o);
    // or
    ApiKeyAuthenticationExtensions.AddApiKeyAuthentication(o, parameterName: "X-Api-Key");
});
```

Make enums display as strings in Swagger:

```csharp
var builder = WebApplication.CreateBuilder();
builder.Services.AddControllers().DisplayEnumAsString();
```

---

## System.Hosting

### WebHostOptions

Configure via `appsettings.json` under `"Hosting"`:

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `ServiceName` | `string?` | `null` | App / Windows Service display name |
| `Mode` | `string` | `"Production"` | Hosting mode (inherited from `HostOptions`; see `HostingModes`) |
| `LocalPort` | `int?` | `null` | Override listening port |
| `SelfHosting` | `bool` | `false` | Flags the app as self-hosted (e.g. Kestrel / Windows Service) |
| `EnableSwagger` | `bool` | `true` | Toggle Swagger UI |
| `EnableCors` | `bool` | `false` | Toggle CORS |
| `EnableHttps` | `bool` | `false` | Toggle HTTPS redirect |
| `RoutePrefix` | `string?` | `null` | API route prefix |

```csharp
var builder = WebApplication.CreateBuilder();
builder.Host.UseWebHostOptions();
```

### Background Tasks

Queue and execute long-running work without blocking requests.

```csharp no-compile
services.UseBackgroundQueue();

// In a controller
public IActionResult StartExport([FromServices] IBackgroundTaskQueue queue)
{
    queue.QueueBackgroundWorkItem(async token =>
    {
        await GenerateReport(token);
    });
    return Accepted();
}
```

Typed tasks with progress tracking:

```csharp no-compile
services.UseBackgroundQueue<ReportTask>();

// inject IBackgroundQueueManager<ReportTask>
var task = queueManager.Execute<string>(async (sp, t) =>
{
    t.SetProgress(0.5);
    return await GenerateReport(sp, t.Id);
});
```

---

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.Web/)** — Overview, template engines, middleware, Swagger, and hosting
1. [Examples](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html) — HTML templating, exception handling, background tasks

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
