# Razor components through ASP.NET Core's HtmlRenderer

As of 2026-10-08. Sources: the `Regira-Packages` repository, branch `wip` at `378431c`; Microsoft Learn, [Render Razor components outside of ASP.NET Core](https://learn.microsoft.com/aspnet/core/blazor/components/render-components-outside-of-aspnetcore), read on 2026-10-08; the nuget.org metadata of Microsoft.AspNetCore.Components.Web (10.0.12), Microsoft.AspNetCore.Razor.Language (last release 6.0.36, published 2024-11-12), Microsoft.CodeAnalysis.Razor.Compiler (one preview, 10.0.0-preview.25277.114), RazorLight 2.3.1 (published 2023-01-16) and RazorEngineCore 2026.1.1 (published 2026-01-17), read on 2026-10-08; and a throwaway spike on .NET 8.0.30 and 10.0.11 (see *What the spike showed*). Nothing on this page has been built.

**Status: proposal, not built. Five decisions are open (see *Decisions*).**

## Recommendation

Add `Regira.Web.HTML.RazorComponents`, a third Razor package beside Web.HTML.RazorLight and Web.HTML.RazorEngineCore. Its `RazorComponentRenderer` renders a Razor component (a `.razor` file) to an HTML string with ASP.NET Core's built-in `HtmlRenderer`. Like its siblings, it turns a Razor template and a model into HTML for a mail body or for HTML to PDF.

The contract differs. `HtmlRenderer` renders a component type that was compiled with the application; it cannot take template text. So the package implements a new `IHtmlComponentRenderer` in Common.Web rather than `IHtmlParser`. Decision 1 weighs the alternatives, including compiling `.razor` text at runtime, which the spike got working.

The gap this closes:

- **No runtime compiler.** RazorLight and RazorEngineCore compile template text with Roslyn at runtime. Both rest on Microsoft's Razor compiler library, which stopped at 6.0.36 in November 2024; the compiler now ships only inside the SDK. A cold first render takes 1.2–1.6 s and adds 60–90 MB of working set. `HtmlRenderer` renders a precompiled component: 60–120 ms cold, about 0.01 ms warm.
- **No new dependency.** `HtmlRenderer` is part of the `Microsoft.AspNetCore.App` shared framework. `Regira.Web` already references it, so the package adds no third-party library and is serviced with .NET.
- **Errors at build time.** A misspelt property or a type error in a template fails the build, not the first render in production.
- **Encoded output.** Values are HTML-encoded, as in RazorLight. RazorEngineCore encodes them only with `Options.HtmlEncode` set, and writes them as markup by default.

What it does not cover: templates that change without a rebuild, such as templates stored in a database, edited by users or deployed as files. Those stay on RazorLight. This proposal replaces neither engine.

The steps, in order:

1. `IHtmlComponentRenderer` in Common.Web.
2. The package: `RazorComponentRenderer`.
3. Tests in `Web.HTML.Testing`.
4. Guides, docs, routing tables, the solution, GuideVerifier and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| `IHtmlParser.Parse<T>(string html, T model)` | `src/Common.Web/HTML/Abstractions/IHtmlParser.cs` | The contract takes template text and reads it at runtime. `HtmlRenderer` cannot take text |
| Three implementations | `HtmlTemplateParser` (token replacement, in Common.Web); `RazorEngineCore.RazorTemplateParser`, which strips `@model` and `Layout = null`; `RazorLight.RazorTemplateParser`, whose `Options.TemplateKey` replaces the cache key with a fixed one | The two Razor parsers cache compiled templates by their text for the whole process: a template compiles on its first call (see *What the spike showed*) and renders from the cache after that |
| `Common.Web` has a `FrameworkReference` to `Microsoft.AspNetCore.App` | `src/Common.Web/Common.Web.csproj` | `HtmlRenderer`, `IComponent` and `ParameterView` are available to Common.Web and, through the transitive framework reference, to every consumer of `Regira.Web` |
| Both engines sit on the Razor 6.0 compiler library | RazorEngineCore 2026.1.1 depends on `Microsoft.AspNetCore.Razor.Language` 6.0.36. RazorLight 2.3.1 depends on `Microsoft.AspNetCore.Mvc.Razor.Extensions` and `Microsoft.CodeAnalysis.Razor` 6.0.0 | Microsoft has not published the Razor compiler as a library since 6.0. nuget.org has a single preview of `Microsoft.CodeAnalysis.Razor.Compiler` |
| The Web.HTML packages ship no DI extension | The docs register `services.AddSingleton<IHtmlParser>(new RazorTemplateParser(...))` | The new package follows suit with one registration line |
| One test project for the Razor engines | `tests/Web.HTML.Testing`, SDK `Microsoft.NET.Sdk`, `.cshtml` templates in `Assets/Input` read as text | The component tests join it. The SDK switch needs care (see *Tests*) |
| The template-to-PDF pipeline is documented on `IHtmlParser` | `src/Common.Web/docs/examples.md` Example 1, `src/Common.Web/ai/web.examples.md`, `src/Common.Office/docs/pdf/examples.md` Example 2 | The component variant gets one worked example in the Web docs, and the PDF example stays as it is |
| The Web packages ship at 6.5.1 | `Common.Web`, `Web.HTML.RazorLight`, `Web.HTML.RazorEngineCore` | The new package starts on the family's number |

## What the spike showed

A console project on the Razor SDK (`Microsoft.NET.Sdk.Razor`), targeting `net8.0` and `net10.0`, rendered compiled components with `HtmlRenderer`. The same template also ran on RazorLight 2.3.1 and RazorEngineCore 2026.1.1, through the engine calls the Regira parsers make. Timings are single runs on one development machine. Each "cold" figure comes from a fresh process with one engine.

**HtmlRenderer**, identical on .NET 8 and .NET 10:

| Case | Result |
|---|---|
| A component with `[Parameter] public Order Model`, an empty service provider and `NullLoggerFactory` | Renders. Cold first render 60–120 ms, working set 34 MB |
| Warm renders | 0.014–0.019 ms on a shared renderer; 0.009 ms with a new renderer per call |
| A long-lived renderer | It keeps every root component it rendered until it is disposed: +37 MB after 20,000 renders. A renderer per call retains nothing |
| 200 parallel renders on one renderer | All correct; the dispatcher serializes them |
| A model value `Order <b>#1</b>` | Encoded as `&lt;b&gt;`. `@((MarkupString)value)` writes markup |
| `OnInitializedAsync` awaiting a 100 ms delay | The HTML shows the final state. `RenderComponentAsync` waits for asynchronous lifecycle methods, as Microsoft's page says |
| A child component, `<OrderLineRow Line="line" />` | Rendered |
| A generic component (`@typeparam TModel`) | Rendered |
| A full document, `<html><body>…</body></html>` | Rendered as written |
| `@inject` of a registered service | Resolved from the provider given to the renderer |
| `@inject NavigationManager` | `InvalidOperationException`: no registered service of type `NavigationManager` |
| `@layout MainLayout` | Ignored when the component is rendered directly. Rendering `LayoutView` with `Layout = typeof(MainLayout)` and the component as `ChildContent` applies it |
| An exception thrown in the template | The component's own exception propagates from `RenderComponentAsync`. The same renderer renders again afterwards |
| An unknown parameter name, or a model of the wrong type | `InvalidOperationException` naming the property |
| `CurrentCulture` | The caller's: `7.5` renders as `7.5` under `en-US` and `7,5` under `fr-FR` |
| Whitespace | Whitespace between elements in code blocks is removed: `<ul><li>…</li><li>…</li></ul>` |
| Built without `PreserveCompilationContext` | Renders; nothing is compiled at runtime |
| The Razor SDK in a project that holds `.cshtml` template files | The SDK compiles them as MVC views and the build fails (`RAZORSDK1004`, then `CS0246` on `@model`). `<EnableDefaultRazorGenerateItems>false</EnableDefaultRazorGenerateItems>` keeps them out, and `.razor` components still compile. `<RazorGenerate Remove="…" />` did not |

**The existing engines**, on the same template:

| Case | RazorLight | RazorEngineCore |
|---|---|---|
| Cold first render | 1.6 s, working set 125 MB | 1.2–1.3 s, working set 94 MB |
| Warm render | 0.11–0.15 ms with a fixed key | — (not measured) |
| A call that compiles again: a new key, a new `CompileAsync` | about 50 ms | about 55 ms |
| 300 such calls | +301 assemblies loaded, working set +59 MB | +300 assemblies loaded, working set +23 MB |
| `Order <b>#1</b>` | Encoded | Written as markup by default; encoded with `Options.HtmlEncode` |

**`.razor` text compiled at runtime.** The test used `Microsoft.AspNetCore.Razor.Language` 6.0.36 and Roslyn 5.0 with `FileKinds.Component`. The template began `@inherits ComponentTemplate<Order>`, a base class with a `Model` parameter, and `HtmlRenderer` rendered the result:

| Case | Result |
|---|---|
| Markup and C# only | Rendered. Cold 1.3–1.7 s, working set 125–138 MB. The class is named from a hash, `__GeneratedComponent.AspNetCore_…` |
| A child component | Not rendered: it is written out as the literal tag `<OrderLineRow Line="line"></OrderLineRow>`, with only warning `RZ10012`. Finding components needs tag-helper discovery over a Roslyn compilation of the referenced assemblies |
| An anonymous model through `dynamic` | `RuntimeBinderException`: `'object' does not contain a definition for 'Name'`. Anonymous types are internal to the caller's assembly |

## Design

### The abstraction, in Common.Web

<!-- no-compile -->
```csharp
namespace Regira.Web.HTML.Abstractions;

public interface IHtmlComponentRenderer
{
    /// <summary>Renders a Razor component to HTML. Each entry sets the component parameter of that name.</summary>
    Task<string> Render(Type componentType, IDictionary<string, object?>? parameters = null);
}

public static class HtmlComponentRendererExtensions
{
    public static Task<string> Render<TComponent>(this IHtmlComponentRenderer renderer, IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
        => renderer.Render(typeof(TComponent), parameters);

    /// <summary>Sets the component's <c>Model</c> parameter: the template-and-model shape of <see cref="IHtmlParser"/>.</summary>
    public static Task<string> Render<TComponent, TModel>(this IHtmlComponentRenderer renderer, TModel model)
        where TComponent : IComponent
        => renderer.Render(typeof(TComponent), new Dictionary<string, object?> { ["Model"] = model });
}
```

The interface has one member, so a test double implements a single method. The `Type` form serves a template that is picked at runtime, such as a map from a document kind to a component type.

### The package

`src/Web.HTML.RazorComponents/`, package and assembly `Regira.Web.HTML.RazorComponents`, `net8.0;net10.0`, version 6.5.1, Apache-2.0. Its only reference is `Common.Web`; it has no `PackageReference`.

<!-- no-compile -->
```csharp
namespace Regira.Web.HTML.RazorComponents;

public class RazorComponentRenderer(IServiceProvider? serviceProvider = null, ILoggerFactory? loggerFactory = null)
    : IHtmlComponentRenderer
{
    private readonly IServiceProvider _services = serviceProvider ?? new ServiceCollection().BuildServiceProvider();

    public async Task<string> Render(Type componentType, IDictionary<string, object?>? parameters = null)
    {
        // A renderer per call: an HtmlRenderer keeps every component it rendered until it is disposed
        await using var renderer = new HtmlRenderer(_services, loggerFactory ?? NullLoggerFactory.Instance);
        var view = parameters == null ? ParameterView.Empty : ParameterView.FromDictionary(parameters);
        return await renderer.Dispatcher.InvokeAsync(async () =>
            (await renderer.RenderComponentAsync(componentType, view)).ToHtmlString());
    }
}
```

- **Without DI**, `new RazorComponentRenderer()` works, as `new RazorTemplateParser()` does for its siblings. A component that uses `@inject` then fails with the renderer's `InvalidOperationException`.
- **With DI**, one line: `services.AddTransient<IHtmlComponentRenderer, RazorComponentRenderer>();`. The renderer takes the provider that resolved it, so `@inject` resolves from the request's scope inside a request. Registered as a singleton, it would hold the root provider, and a scoped service such as a `DbContext` fails under scope validation.
- **Exceptions are not wrapped.** No template compiles at runtime, so the only failures are the template's own exceptions and the renderer's `InvalidOperationException` for a parameter it cannot set.

### Usage

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
```

The template body is the same as the RazorLight example's `.cshtml`. A port replaces `@model InvoiceDto` with the `[Parameter]` property.

### What a consumer's project needs

- **The Razor SDK.** The `.razor` files compile in the consumer's project. An ASP.NET Core app (`Microsoft.NET.Sdk.Web`) does that already. A worker, console app or class library switches its SDK to `Microsoft.NET.Sdk.Razor`.
- **Existing `.cshtml` template files.** A project that switches to the Razor SDK and still holds RazorLight or RazorEngineCore templates as files sets `EnableDefaultRazorGenerateItems` to `false`.
- **No `PreserveCompilationContext`.**

### Behaviour the guide documents

- **Encoding.** Values are HTML-encoded. `@((MarkupString)html)` writes markup, where RazorLight, and RazorEngineCore with `HtmlEncode`, use `@Raw(html)`.
- **Async.** `OnInitializedAsync` and `OnParametersSetAsync` are awaited before the HTML is returned.
- **Layouts.** `@layout` has no effect. A shared document frame is a component with a `ChildContent` parameter that wraps the content, or the call renders `LayoutView`.
- **HTML only.** No `NavigationManager`, no event handlers, no render modes, no JavaScript interop.
- **Services.** `@inject` resolves from the provider the renderer was given.
- **Culture.** The caller's `CurrentCulture`, so `UseRequestCulture` applies to the rendered HTML.
- **Whitespace.** Whitespace between elements is trimmed. `@preservewhitespace true` keeps it.

### The guide's comparison table gains a row

| Engine | Class | When to use |
|---|---|---|
| Razor components | `Regira.Web.HTML.RazorComponents` | Templates that ship with the app: checked at build time, no runtime compiler, no third-party dependency. Not for templates that change without a rebuild |

## Tests

In `tests/Web.HTML.Testing`:

- **The project switches to `Microsoft.NET.Sdk.Razor`** and sets `EnableDefaultRazorGenerateItems` to `false`, so the `.cshtml` inputs in `Assets/Input` remain text files for the other two engines (see the spike).
- **Components:** `Templates/SimpleRazor.razor` and `Templates/RazorOrder.razor`, which mirror `simple-razor.cshtml` and `razor-order.cshtml` with the same `Order` and `OrderLine` models and the Regira logo.
- **`RazorComponentTests`:**
  - `Simple_Razor_Template` and `Razor_Order_Model`, with the siblings' assertions, writing `Assets/Output/razor-order-components.html`.
  - Encoding of a model value that holds markup.
  - An async lifecycle method.
  - `@inject` from a provider, and the `InvalidOperationException` without one.
  - A template's exception, propagated unchanged.
- **No external requirements.**

## Files to change

| File | Change |
|---|---|
| `src/Common.Web/HTML/Abstractions/IHtmlComponentRenderer.cs` | New: the interface and its extensions |
| `src/Web.HTML.RazorComponents/` | New: `.csproj`, `RazorComponentRenderer.cs`, `README.md` (copy Web.HTML.RazorLight's) |
| `src/Common.Web/ai/web.instructions.md` | Projects table, installation, a section on the renderer with the behaviour list above, the comparison table row |
| `src/Common.Web/README.md` | Projects table, installation, a subsection beside RazorLight's |
| `src/Common.Web/docs/examples.md` | One example: the invoice as a component, feeding HTML to PDF |
| `ai/AGENTS.md`, `src/Common.Setup/ai/copilot-instructions.md` | The `Regira.Web*` routing rows list the package and say when to pick it over RazorLight. Both files have uncommitted edits from other work on `wip` today |
| `Regira-Packages.slnx` | The project |
| `tools/GuideVerifier/projects.json` | The `web` group's project list. A C# snippet that names a component type compiles only against a stub, so those snippets stay `<!-- no-compile -->` fragments, and the `razor` fences are not compiled |
| `tests/Web.HTML.Testing/` | The SDK switch, the components, the fixture, `README.md` |
| `CHANGELOG.md` | `` `Regira.Web.HTML.RazorComponents` 6.5.1 `` (new package) and `` `Regira.Web` 6.5.1 `` (`IHtmlComponentRenderer`). It has uncommitted edits from other work on `wip` today |

Regira-Website's `packages.json` picks the package up from `src/` the next time `npm run packages` runs there.

## Decisions

1. **The contract.** Recommended: a new `IHtmlComponentRenderer` that renders compiled components. Alternatives:
   - **`IHtmlParser`, compiling `.razor` text at runtime.** The spike shows that it works for markup and C#. But it is a third runtime-compiling engine with RazorLight's costs: 1.3–1.7 s cold, the frozen Razor 6.0 compiler library, a new assembly per template. Child components come out as literal tags, and anonymous models fail. Component syntax is not `.cshtml` syntax either, so existing templates do not port unchanged. It gives up every reason to use `HtmlRenderer`.
   - **An `IHtmlParser` adapter whose `html` argument names a registered component.** It misreads the parameter, since every existing call passes template text.
2. **Where the code lives.** Recommended: the interface in Common.Web beside `IHtmlParser`, and the renderer in the new package, as asked. A consumer then installs one package per engine, and services depend on the hub. Alternatives:
   - **Both in Common.Web.** The renderer needs nothing Common.Web lacks, and `HtmlTemplateParser` sets the precedent for an engine with no dependency living in the hub. It saves a package, but every `Regira.Web` consumer gets it.
   - **Both in the new package.** Common.Web stays unchanged, but the abstraction belongs to its only provider.
3. **Names.** Recommended: package `Regira.Web.HTML.RazorComponents`, class `RazorComponentRenderer`, interface `IHtmlComponentRenderer`. "Razor components" is Microsoft's name for the technology, and "Razor" lines the package up with its siblings. Alternatives:
   - **`Regira.Web.HTML.HtmlRenderer`.** It names the class, as the siblings name their library. But inside a namespace that ends in `HtmlRenderer`, the simple name `HtmlRenderer` resolves to the namespace (`CS0118`), so the package's own code needs an alias.
   - **`Regira.Web.HTML.Blazor`.** It is the term people search for, but no Blazor hosting model is involved.
4. **The model convenience.** Recommended: `Render<TComponent, TModel>(model)` sets the `Model` parameter, and the template declares that parameter itself in plain Blazor. Alternatives:
   - **`Render<TComponent>(object model)`.** It reads shorter, but a dictionary passed as the model binds to the parameters overload instead.
   - **A Regira base class**, `RazorComponentTemplate<TModel>` with the `Model` parameter, declared as `@inherits RazorComponentTemplate<InvoiceDto>`. It mirrors `@model`, but puts a Regira type in every template.
5. **Renderer lifetime.** Recommended: a new `HtmlRenderer` per call, and a transient registration in the docs. Alternatives:
   - **A shared renderer.** It retains every rendered component, +37 MB over 20,000 renders, and the spike measured it as no faster.
   - **An `AddRazorComponentRenderer()` extension.** The sibling packages ship none.

## Not in scope

- **Compiling `.razor` text at runtime** (decision 1).
- **Streaming output.** `HtmlRootComponent.WriteHtmlTo(TextWriter)` and `BeginRenderingComponent` could back a member that writes to a stream later.
- **The existing parsers.** The spike found two things in them, both outside this package:
  - Each compile loads an assembly that is never unloaded. Both parsers compile each distinct template text once per process; `tests/Web.HTML.Testing/TemplateCompilationTests.cs` counts the assemblies.
  - RazorEngineCore writes model values as markup by default. `Options.HtmlEncode` encodes them with RazorLight's encoder, and `@Raw(value)` writes markup; `tests/Web.HTML.Testing/RazorEngineCoreTemplateTests.cs` covers both.
