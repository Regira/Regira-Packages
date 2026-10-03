# Web.HTML.Testing

Tests for the Razor template parsers [Web.HTML.RazorLight](../../src/Web.HTML.RazorLight/README.md) and
[Web.HTML.RazorEngineCore](../../src/Web.HTML.RazorEngineCore/README.md): rendering the `.cshtml` templates in
`Assets/Input` with a model, writing the HTML to `Assets/Output`. NUnit.

## Running

```bash
dotnet test tests/Web.HTML.Testing
```

No external requirements. The project sets `PreserveCompilationContext` and `PreserveCompilationReferences` so the
templates can compile against the test assembly's references; keep both when editing the project file.
