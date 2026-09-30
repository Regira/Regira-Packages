# Regira.Web.HTML.RazorEngineCore

Razor template engine for [Regira Web.HTML](https://regira.github.io/Regira-Packages/src/Common.Web/), built on [RazorEngineCore](https://www.nuget.org/packages/RazorEngineCore). `RazorTemplateParser` implements `IHtmlParser` and renders a Razor template string with a model to HTML. The engine has no layout support: `@model` directives and `Layout = null` blocks are stripped before compiling, so it suits simple templates without layout inheritance.

## Installation

```xml
<PackageReference Include="Regira.Web.HTML.RazorEngineCore" Version="6.*" />
```

## Documentation

- [HTML Template Parsing](https://regira.github.io/Regira-Packages/src/Common.Web/#html-template-parsing) — the shared `IHtmlParser` contract and how the three template engines differ
- [Examples](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html) — a Razor invoice template rendered through `IHtmlParser`, and HTML-to-PDF

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
