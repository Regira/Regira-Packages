# Regira Web
<!-- {% raw %} -->

Regira Web provides Razor-based HTML template rendering plus common web utilities, middleware, and Swagger configuration.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.Web` | `Regira.Web` | Core web utilities, middleware, exception handling |
| `Web.Analytics` | `Regira.Web.Analytics` | Abstract visitor analytics — pluggable capture, enrichment, and storage hooks |
| `Web.Analytics.GeoIP2` | `Regira.Web.Analytics.GeoIP2` | Country/city enrichment from a local MaxMind GeoIP2/GeoLite2 database |
| `Web.HTML.RazorEngineCore` | `Regira.Web.HTML.RazorEngineCore` | Razor templates via RazorEngineCore |
| `Web.HTML.RazorLight` | `Regira.Web.HTML.RazorLight` | Razor templates via RazorLight |
| `Web.Swagger` | `Regira.Web.Swagger` | Swagger/OpenAPI JWT & API Key support |

Host configuration (`WebHostOptions`), background task queues and the Windows Service installer live in `Regira.System.Hosting` — see [System](https://regira.github.io/Regira-Packages/src/Common.System/docs/hosting.html).

## Installation

```xml
<!-- Core web utilities -->
<PackageReference Include="Regira.Web" Version="6.*" />

<!-- Visitor analytics (+ optional geolocation) -->
<PackageReference Include="Regira.Web.Analytics" Version="6.*" />
<PackageReference Include="Regira.Web.Analytics.GeoIP2" Version="6.*" />

<!-- Razor templates (pick one) -->
<PackageReference Include="Regira.Web.HTML.RazorEngineCore" Version="6.*" />
<PackageReference Include="Regira.Web.HTML.RazorLight" Version="6.*" />

<!-- Swagger -->
<PackageReference Include="Regira.Web.Swagger" Version="6.*" />
```

---

## HTML Template Parsing

### IHtmlParser

<!-- no-compile -->
```csharp
Task<string> Parse<T>(string html, T model);
```

All three implementations share this interface.

### HtmlTemplateParser (simple placeholder engine)

Replaces `{key}` tokens with the model's values. The model goes through the `ISerializer` first, so a key is the serialized name: Regira's JSON serializer camel-cases property names (`Name` → `{name}`, `Customer.Name` → `{customer.name}`) and keeps dictionary keys as written. A token with no value — a missing key, or a null the serializer leaves out — stays in the output as written.

A comment-marked block repeats its content once for each item of a collection, with that item's keys and a 1-based `{rowNr}`. Its name is letters only and starts lowercase. A block is not a condition: a `true` flag renders nothing. `{key:format}` formats a number inside a block; the optional `valueConverter` turns each top-level value into text (`ToString()` by default) and never sees block values.

```html
<h1>{title}</h1>
<ul>
<!--{{orderLines}}-->
<li>{rowNr}. {title}: {price:0.00}</li>
<!--{{/orderLines}}-->
</ul>
```

By default values are inserted as they are, so a value holding `<b>` comes out as markup. `HtmlEncode = true` encodes every text value, at the top level and in blocks; set it whenever the model carries user-supplied text. `{key:raw}` then writes a value unencoded.

```csharp
ISerializer jsonSerializer = new JsonSerializer();
string template = "<p>Hello {name}!</p>";

var parser = new HtmlTemplateParser(jsonSerializer) { HtmlEncode = true };
string html = await parser.Parse(template, new { Name = "Alice" });
```

### RazorEngineCore

Full Razor syntax. Strips `@model` directives and `Layout` blocks (not supported by the engine). Best for simple templates without layout inheritance.

By default it writes model values as they are, so a value holding `<b>` comes out as markup. `HtmlEncode = true` encodes every value a template writes with `@`, in text and in attribute values, the way RazorLight does; set it whenever the model carries user-supplied text. `@Raw(value)` then writes a value unencoded.

```csharp
string razorTemplate = "<p>Hello @Model.Name</p>";
var model = new { Name = "Alice" };

IHtmlParser parser = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser(new()
{
    HtmlEncode = true
});
string html = await parser.Parse(razorTemplate, model);
```

### RazorLight

Full Razor syntax, with the `@model` directive compiled as written. Encodes every value a template writes with `@`; `@Raw(value)` writes a value unencoded.

```csharp
string razorTemplate = "<p>Hello @Model.Name</p>";
var model = new { Name = "Alice" };

IHtmlParser parser = new Regira.Web.HTML.RazorLight.RazorTemplateParser();
string html = await parser.Parse(razorTemplate, model);
```

### Compiled Razor templates

Both Razor parsers compile a template into an assembly that stays loaded until the process exits, and cache it by the template text for the whole process: each distinct template compiles once, whichever parser instance renders it, and later calls only render. Keep the template text fixed and pass the data as the model. Data concatenated into the text makes a new template on every call, which compiles every time and grows memory without bound.

RazorLight's `Options.TemplateKey` replaces the text with a fixed key, cached per parser instance. The parser renders the first template it compiled on every later call, even when it is passed a different template, and each new instance compiles again. Leave it unset; a parser that does set one must get a single template and be registered as a singleton.

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

### ControllerExtensions and FormFileExtensions

<!-- no-compile -->
```csharp
// Return INamedFile as a download or inline
return this.File(namedFile, inline: true);

// Read an upload as an INamedFile
var upload = formFile.ToNamedFile();
```

- `File()` sends `X-Content-Type-Options: nosniff` and, with every file but a PDF, `Content-Security-Policy:
  sandbox`, so a file served from the app's origin renders but runs no script, whatever its name or stored type. A PDF
  goes without: a sandbox keeps the browser's PDF viewer from loading. The sandbox is added to a policy the app
  already sent, not put in its place; a middleware that sets `Content-Security-Policy` once the response starts
  (`OnStarting`) must append to the header too, or it replaces the sandbox.
- `ToNamedFile()` sets `ContentType` from the file name's extension (`application/octet-stream` when unknown), never
  from the type the client declared. Restrict uploads by extension where it matters.

### RequestUtility

Extension methods on `HttpRequest`:

<!-- no-compile -->
```csharp
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

## Overview

1. **[Index](https://regira.github.io/Regira-Packages/src/Common.Web/)** — Overview, template engines, middleware, and Swagger
1. [Examples](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html) — HTML templating, exception handling, background tasks

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

<!-- {% endraw %} -->
