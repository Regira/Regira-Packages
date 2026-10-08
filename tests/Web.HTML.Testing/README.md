# Web.HTML.Testing

Tests for the Razor template parsers [Web.HTML.RazorLight](../../src/Web.HTML.RazorLight/README.md) and
[Web.HTML.RazorEngineCore](../../src/Web.HTML.RazorEngineCore/README.md): rendering the `.cshtml` templates in
`Assets/Input` with a model, writing the HTML to `Assets/Output`. NUnit.

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
