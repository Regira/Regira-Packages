# Web.HTML.Testing

Tests for the Razor template parsers [Web.HTML.RazorLight](../../src/Web.HTML.RazorLight/README.md) and
[Web.HTML.RazorEngineCore](../../src/Web.HTML.RazorEngineCore/README.md): rendering the `.cshtml` templates in
`Assets/Input` with a model, writing the HTML to `Assets/Output`. NUnit.

`RazorComponentTests` covers [Web.HTML.RazorComponents](../../src/Web.HTML.RazorComponents/README.md): the components in
`Templates` compile with the project, and `SimpleRazor.razor` and `RazorOrder.razor` mirror the two `.cshtml` templates
with the same `Order` model. The fixture also covers child and generic components, encoding, an async lifecycle method,
`@inject`, and the exceptions a render passes on.

`HtmlTemplateParserTests` covers the token parser in [Regira.Web](../../src/Common.Web/README.md) — `{key}` tokens,
repeating blocks and `HtmlEncode` — with the Newtonsoft `JsonSerializer` that consumers pass it.

`TemplateCompilationTests` checks that each template compiles once: a compiled template is an assembly that is never
unloaded, so the tests count the assemblies loaded into the process. The fixture is `NonParallelizable`, since a
template compiled by a concurrent test would be counted too.

## Running

```bash
dotnet test tests/Web.HTML.Testing
```

No external requirements. The project sets `PreserveCompilationContext` and `PreserveCompilationReferences` so the
templates can compile against the test assembly's references; keep both when editing the project file.

The project uses the Razor SDK (`Microsoft.NET.Sdk.Razor`) so the `.razor` components compile. It sets
`EnableDefaultRazorGenerateItems` to `false`, so the `.cshtml` files in `Assets/Input` stay template text: otherwise the
SDK takes them for MVC views and warns with `RAZORSDK1004`.
