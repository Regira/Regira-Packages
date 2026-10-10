# Regira Web AI Agent Instructions
<!-- {% raw %} -->

> Razor-based HTML template rendering plus common web utilities, middleware, and Swagger configuration.

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.Web` | `Regira.Web` | Core web utilities, middleware, exception handling |
| `Web.Analytics` | `Regira.Web.Analytics` | Abstract visitor analytics — pluggable capture, enrichment, and storage hooks |
| `Web.Analytics.GeoIP2` | `Regira.Web.Analytics.GeoIP2` | Country/city enrichment from a local MaxMind GeoIP2/GeoLite2 database |
| `Web.HTML.RazorComponents` | `Regira.Web.HTML.RazorComponents` | Razor components (`.razor`) compiled with the app, rendered via ASP.NET Core's `HtmlRenderer` |
| `Web.HTML.RazorEngineCore` | `Regira.Web.HTML.RazorEngineCore` | Razor templates via RazorEngineCore |
| `Web.HTML.RazorLight` | `Regira.Web.HTML.RazorLight` | Razor templates via RazorLight |
| `Web.Swagger` | `Regira.Web.Swagger` | Swagger/OpenAPI JWT & API Key support |

---

## Installation

```xml
<!-- Core web utilities and middleware -->
<PackageReference Include="Regira.Web" Version="6.*" />

<!-- Visitor analytics (+ optional geolocation) -->
<PackageReference Include="Regira.Web.Analytics" Version="6.*" />
<PackageReference Include="Regira.Web.Analytics.GeoIP2" Version="6.*" />

<!-- Razor templates (pick one) -->
<PackageReference Include="Regira.Web.HTML.RazorEngineCore" Version="6.*" />
<PackageReference Include="Regira.Web.HTML.RazorLight" Version="6.*" />

<!-- Razor components compiled with the app -->
<PackageReference Include="Regira.Web.HTML.RazorComponents" Version="6.*" />

<!-- Swagger JWT + API Key support -->
<PackageReference Include="Regira.Web.Swagger" Version="6.*" />

```

---

## HTML Template Parsing

### `IHtmlParser`

<!-- no-compile -->
```csharp
Task<string> Parse<T>(string html, T model);
```

All three implementations share this interface.

---

### `HtmlTemplateParser` — simple `{token}` placeholders

Replaces `{key}` tokens with the model's values. The model goes through the `ISerializer` first, so a key is the
serialized name: Regira's JSON serializer camel-cases property names (`Name` → `{name}`, `Customer.Name` →
`{customer.name}`) and keeps dictionary keys as written. A token with no value — a missing key, or a null the serializer leaves out — stays in the output as written.

A block repeats its content once for each item of a collection, with that item's keys and a 1-based `{rowNr}`. Its
name is letters only and starts lowercase. A block is not a condition: a `true` flag renders nothing.
`{key:format}` formats a number inside a block; the optional `valueConverter` turns each top-level value into text
(`ToString()` by default) and never sees block values.

```html
<h1>{title}</h1>
<ul>
<!--{{orderLines}}-->
<li>{rowNr}. {title}: {price:0.00}</li>
<!--{{/orderLines}}-->
</ul>
```

By default values are inserted as they are, so a value holding `<b>` comes out as markup. `HtmlEncode = true`
encodes every text value, at the top level and in blocks; set it whenever the model carries user-supplied text.
`{key:raw}` then writes a value unencoded.

<!-- no-compile -->
```csharp
var parser = new HtmlTemplateParser(jsonSerializer) { HtmlEncode = true };
string html = await parser.Parse(template, order);
```

---

### `RazorEngineCore.RazorTemplateParser` — full Razor syntax

Best for simple templates without layout inheritance. Strips `@model` directives and `Layout` blocks.

By default it writes model values as they are, so a value holding `<b>` comes out as markup. `HtmlEncode = true`
encodes every value a template writes with `@`, in text and in attribute values, the way RazorLight does; set it
whenever the model carries user-supplied text. `@Raw(value)` then writes a value unencoded.

<!-- no-compile -->
```csharp
IHtmlParser parser = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser(new()
{
    HtmlEncode = true
});
string html = await parser.Parse(razorTemplate, model);
```

---

### `RazorLight.RazorTemplateParser` — full Razor syntax

Compiles the `@model` directive as written. Encodes every value a template writes with `@`; `@Raw(value)` writes a
value unencoded.

<!-- no-compile -->
```csharp
IHtmlParser parser = new Regira.Web.HTML.RazorLight.RazorTemplateParser();
string html = await parser.Parse(razorTemplate, model);
```

---

### Compiled Razor templates

Both Razor parsers compile a template into an assembly that stays loaded until the process exits, and cache it by the template text for the whole process: each distinct template compiles once, whichever parser instance renders it, and later calls only render. Keep the template text fixed and pass the data as the model. Data concatenated into the text makes a new template on every call, which compiles every time and grows memory without bound.

RazorLight's `Options.TemplateKey` replaces the text with a fixed key, cached per parser instance. The parser renders the first template it compiled on every later call, even when it is passed a different template, and each new instance compiles again. Leave it unset; a parser that does set one must get a single template and be registered as a singleton.

---

## Razor Components

`Regira.Web.HTML.RazorComponents` renders a Razor component — a `.razor` file compiled with the application — to HTML
with ASP.NET Core's `HtmlRenderer`. It turns a template and a model into HTML as the parsers above do, but it takes a
component type, not template text, so it implements `IHtmlComponentRenderer` rather than `IHtmlParser`. A template is
checked at build time and nothing compiles at runtime. Templates that change without a rebuild — stored in a database,
edited by users, deployed as files — need RazorLight instead.

### `IHtmlComponentRenderer`

<!-- no-compile -->
```csharp
Task<string> Render(Type componentType, IDictionary<string, object?>? parameters = null);

// Extensions
Task<string> Render<TComponent>(IDictionary<string, object?>? parameters = null);
Task<string> Render<TComponent, TModel>(TModel model);   // sets the component's Model parameter
```

Each dictionary entry sets the component parameter of that name. The `Type` form serves a template picked at runtime.

### Registration

```csharp
using Regira.Web.HTML.RazorComponents;

services.AddTransient<IHtmlComponentRenderer, RazorComponentRenderer>();
```

Register it transient. The renderer takes the provider that resolves it, so `@inject` resolves from the request's
scope; as a singleton it holds the root provider, and a scoped service such as a `DbContext` fails. Without DI,
`new RazorComponentRenderer()` renders any component that injects nothing.

### A template

The model is a parameter named `Model`, which `Render<TComponent, TModel>` sets. `Templates/InvoiceTemplate.razor`:

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

A `.cshtml` template ports by replacing `@model InvoiceDto` with the `[Parameter]` property and `@Raw(value)` with
`@((MarkupString)value)`.

### Project setup

The `.razor` files compile in the consuming project, which needs the Razor SDK: `Microsoft.NET.Sdk.Web` has it, and a
worker, console app or class library uses `Microsoft.NET.Sdk.Razor`. A project on the Razor SDK that also holds
`.cshtml` template files for RazorLight or RazorEngineCore sets
`<EnableDefaultRazorGenerateItems>false</EnableDefaultRazorGenerateItems>`, so the SDK leaves them as template text
rather than taking them for MVC views. `PreserveCompilationContext` is not needed.

### Rendering behaviour

- **Encoding.** Values are HTML-encoded. `@((MarkupString)value)` writes markup.
- **Async.** `OnInitializedAsync` and `OnParametersSetAsync` are awaited before the HTML is returned.
- **Components.** Child components and generic components (`@typeparam`) render.
- **Layouts.** `@layout` has no effect. A shared document frame is a component with a `ChildContent` parameter that
  wraps the content.
- **HTML only.** No `NavigationManager`, no event handlers, no render modes, no JavaScript interop.
- **Culture.** The caller's `CurrentCulture`, so `UseRequestCulture` applies to the rendered HTML.
- **Whitespace.** Whitespace between elements in code blocks is trimmed; `@preservewhitespace true` keeps it.
- **Exceptions** pass through unwrapped: the template's own, and `InvalidOperationException` for a parameter the
  component does not declare or an `@inject` service the provider cannot resolve.
- **Memory.** Each call renders on its own `HtmlRenderer` and disposes it, so nothing is retained between calls.

---

## Template Engine Comparison

| Engine | Class | When to use | HTML-encodes model values |
|---|---|---|---|
| `HtmlTemplateParser` | `Regira.Web.HTML` | Simple token replacement, no Razor | Only with `HtmlEncode = true`; `{key:raw}` writes markup |
| `RazorEngineCore` | `Regira.Web.HTML.RazorEngineCore` | Full Razor, no layout support | Only with `HtmlEncode = true`; `@Raw(value)` writes markup |
| `RazorLight` | `Regira.Web.HTML.RazorLight` | Full Razor, keeps `@model` | Yes; `@Raw(value)` writes markup |
| Razor components | `Regira.Web.HTML.RazorComponents` | Templates that ship with the app: checked at build time, no runtime compiler, no third-party dependency. Not for templates that change without a rebuild | Yes; `@((MarkupString)value)` writes markup |

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

### `ControllerExtensions`, `NamedFileResultExtensions` and `FormFileExtensions`

<!-- no-compile -->
```csharp
return this.File(namedFile, inline: true);              // return INamedFile as download or inline
return namedFile.ToFileResult(httpContext, inline: true); // the same from a minimal-API endpoint, as an IResult
var upload = formFile.ToNamedFile();                    // IFormFile → INamedFile
```

- `File()` and `ToFileResult()` answer 404 for a file without content, and send `X-Content-Type-Options: nosniff` and,
  with every file but a PDF, `Content-Security-Policy: sandbox`, so a file served from the app's origin renders but runs no script, whatever its name or stored type. A PDF
  goes without: a sandbox keeps the browser's PDF viewer from loading. The sandbox is added to a policy the app
  already sent, not put in its place; a middleware that sets `Content-Security-Policy` once the response starts
  (`OnStarting`) must append to the header too, or it replaces the sandbox.
- `ToNamedFile()` sets `ContentType` from the file name's extension (`application/octet-stream` when unknown), never
  from the type the client declared. Restrict uploads by extension where it matters.

### `RequestUtility` — `HttpRequest` extension methods

<!-- no-compile -->
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

## Host config, background tasks, Windows Service

`WebHostOptions` (`UseWebHostOptions`), the background task queue (`UseBackgroundQueue`,
`IBackgroundTaskQueue`, `IBackgroundQueueManager<TTask>`) and `AddWindowsServiceInstaller` ship in
**`Regira.System.Hosting`**, not in this family — `Regira.Web` does not reference it. Read
`get_package(id: "Regira.System", section: "system.instructions")`, or `system.instructions.md`
locally, before wiring any of them.

---

<!-- {% endraw %} -->
