# Regira.Web.HTML.RazorLight

Razor template engine for [Regira Web](https://regira.github.io/Regira-Packages/src/Common.Web/), built on [RazorLight](https://www.nuget.org/packages/RazorLight). `RazorTemplateParser` implements `IHtmlParser` and renders a Razor template string with a model to HTML. Compiled templates are cached by their text, so each distinct template compiles once per process.

## Installation

```xml
<PackageReference Include="Regira.Web.HTML.RazorLight" Version="6.*" />
```

- The host project sets `<PreserveCompilationContext>true</PreserveCompilationContext>`; without it the first `Parse` throws `RazorLightException`.
- A template compiles to C# and runs in your process with no sandbox: let only trusted administrators edit one.

## Documentation

- [HTML Template Parsing](https://regira.github.io/Regira-Packages/src/Common.Web/#html-template-parsing) — the shared `IHtmlParser` contract, how the three template engines differ, template caching
- [Render a Razor invoice template](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html#example-1-render-a-razor-invoice-template) — a RazorLight parser registered as a singleton, feeding HTML-to-PDF

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
