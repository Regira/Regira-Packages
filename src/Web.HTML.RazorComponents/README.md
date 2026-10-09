# Regira.Web.HTML.RazorComponents

Razor component renderer for [Regira Web](https://regira.github.io/Regira-Packages/src/Common.Web/), built on ASP.NET Core's `HtmlRenderer`. `RazorComponentRenderer` implements `IHtmlComponentRenderer` and renders a Razor component (a `.razor` file compiled with the application) with its parameters to HTML. Templates are checked at build time and nothing compiles at runtime, so a first render takes milliseconds. Model values are HTML-encoded; `@((MarkupString)value)` writes markup. The package has no third-party dependency: `HtmlRenderer` ships in the `Microsoft.AspNetCore.App` shared framework. Templates that change without a rebuild, such as templates stored in a database, need a runtime engine such as Regira.Web.HTML.RazorLight instead.

## Installation

```xml
<PackageReference Include="Regira.Web.HTML.RazorComponents" Version="6.*" />
```

The `.razor` files compile in the consuming project, which needs the Razor SDK: `Microsoft.NET.Sdk.Web` has it, and a worker, console app or class library uses `Microsoft.NET.Sdk.Razor`.

## Documentation

- [Razor components](https://regira.github.io/Regira-Packages/src/Common.Web/#razor-components) — the `IHtmlComponentRenderer` contract, registration, and how rendering behaves: encoding, async lifecycle methods, layouts, services and culture
- [Render an invoice component](https://regira.github.io/Regira-Packages/src/Common.Web/docs/examples.html#example-6-render-an-invoice-component) — the invoice as a component, feeding HTML-to-PDF

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
