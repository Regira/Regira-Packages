# Regira Web
<!-- {% raw %} -->

Regira Web provides Razor-based HTML template rendering plus common web utilities, middleware, and Swagger configuration.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.Web` | `Regira.Web` | Core web utilities, middleware, exception handling |
| `Web.Analytics` | `Regira.Web.Analytics` | Abstract visitor analytics — pluggable capture, enrichment, and storage hooks |
| `Web.Analytics.GeoIP2` | `Regira.Web.Analytics.GeoIP2` | Country/city enrichment from a local MaxMind GeoIP2/GeoLite2 database |
| `Web.HTML.RazorComponents` | `Regira.Web.HTML.RazorComponents` | Razor components (`.razor`) compiled with the app, rendered via ASP.NET Core's `HtmlRenderer` |
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

<!-- Razor components compiled with the app -->
<PackageReference Include="Regira.Web.HTML.RazorComponents" Version="6.*" />

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

## Razor Components

`Regira.Web.HTML.RazorComponents` renders a Razor component — a `.razor` file compiled with the application — to HTML with ASP.NET Core's `HtmlRenderer`. It takes a component type rather than template text, so `RazorComponentRenderer` implements `IHtmlComponentRenderer` rather than `IHtmlParser`. A template is checked at build time, nothing compiles at runtime, and the package has no third-party dependency. Templates that change without a rebuild — stored in a database, edited by users, deployed as files — need RazorLight instead.

### IHtmlComponentRenderer

<!-- no-compile -->
```csharp
Task<string> Render(Type componentType, IDictionary<string, object?>? parameters = null);

// Extensions
Task<string> Render<TComponent>(IDictionary<string, object?>? parameters = null);
Task<string> Render<TComponent, TModel>(TModel model);   // sets the component's Model parameter
```

Each dictionary entry sets the component parameter of that name; the `Type` form serves a template picked at runtime.

### Registration

```csharp
using Regira.Web.HTML.RazorComponents;

var services = new ServiceCollection();
services.AddTransient<IHtmlComponentRenderer, RazorComponentRenderer>();
```

Register it transient: the renderer takes the provider that resolves it, so `@inject` resolves from the request's scope. As a singleton it would hold the root provider, and a scoped service such as a `DbContext` fails. Without DI, `new RazorComponentRenderer()` renders any component that injects nothing.

### A template

The model is a parameter named `Model`, which `Render<TComponent, TModel>` sets:

```razor
<h1>Invoice #@Model.Number</h1>
<p>Customer: @Model.CustomerName</p>

@code {
    [Parameter, EditorRequired] public InvoiceDto Model { get; set; } = null!;
}
```

<!-- no-compile -->
```csharp
string html = await renderer.Render<InvoiceTemplate, InvoiceDto>(invoice);
```

A `.cshtml` template ports by replacing `@model InvoiceDto` with the `[Parameter]` property and `@Raw(value)` with `@((MarkupString)value)`.

The `.razor` files compile in the consuming project, which needs the Razor SDK: `Microsoft.NET.Sdk.Web` has it, and a worker, console app or class library uses `Microsoft.NET.Sdk.Razor`. A project on the Razor SDK that also holds `.cshtml` template files for RazorLight or RazorEngineCore sets `<EnableDefaultRazorGenerateItems>false</EnableDefaultRazorGenerateItems>`, so the SDK leaves them as template text rather than taking them for MVC views.

### Rendering behaviour

- **Encoding.** Values are HTML-encoded; `@((MarkupString)value)` writes markup.
- **Async.** `OnInitializedAsync` and `OnParametersSetAsync` are awaited before the HTML is returned.
- **Components.** Child components and generic components (`@typeparam`) render.
- **Layouts.** `@layout` has no effect. A shared document frame is a component with a `ChildContent` parameter that wraps the content.
- **HTML only.** No `NavigationManager`, no event handlers, no render modes, no JavaScript interop.
- **Culture.** The caller's `CurrentCulture`, so `UseRequestCulture` applies to the rendered HTML.
- **Whitespace.** Whitespace between elements in code blocks is trimmed; `@preservewhitespace true` keeps it.
- **Exceptions** pass through unwrapped: the template's own, and `InvalidOperationException` for a parameter the component does not declare or an `@inject` service the provider cannot resolve.
- **Memory.** Each call renders on its own `HtmlRenderer` and disposes it, so nothing is retained between calls.

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
1. [Examples](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html) — HTML templating, exception handling, background tasks, Razor components

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).

<!-- {% endraw %} -->
