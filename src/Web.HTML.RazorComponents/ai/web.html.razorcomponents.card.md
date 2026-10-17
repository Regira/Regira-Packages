# Regira.Web.HTML.RazorComponents — index card

> Renders a Razor component (a `.razor` file compiled with the application) to an HTML string with ASP.NET Core's
> `HtmlRenderer`: a mail body, the HTML of a PDF, any template that ships with the app. The guide lives on the hub
> `Regira.Web`: `get_package(id: "Regira.Web", section: "web.instructions", heading: "Razor Components")`.

- **Register** `services.AddTransient<IHtmlComponentRenderer, RazorComponentRenderer>()`: `IHtmlComponentRenderer` in
  `Regira.Web.HTML.Abstractions`, `RazorComponentRenderer` in `Regira.Web.HTML.RazorComponents`. The package has no
  `Add…` extension. Transient, so `@inject` resolves from the request's scope; as a singleton it holds the root
  provider and a scoped service (a `DbContext`) fails. Without DI, `new RazorComponentRenderer()` renders a component
  that injects nothing.
- **Render** with `await renderer.Render<TComponent, TModel>(model)`, which sets the component's `Model` parameter
  (`[Parameter] public TModel Model { get; set; }`). `Render<TComponent>(parameters)` and `Render(Type, parameters)`
  set parameters by name. The generic forms are extensions beside the interface, in `Regira.Web.HTML.Abstractions`.
- **The `.razor` files compile in the consuming project**, which needs the Razor SDK: `Microsoft.NET.Sdk.Web` has it; a
  worker, console app or class library uses `Microsoft.NET.Sdk.Razor`.
- **Values are HTML-encoded.** `@((MarkupString)value)` writes markup as it is, so user input never goes through it.
- **HTML only:** `@layout` has no effect, and there are no event handlers or JavaScript interop. The HTML renders in the
  caller's culture, so a reactor, job or seeder sets the culture it should render in.
- A template that changes without a rebuild (stored or edited by users) needs `Regira.Web.HTML.RazorLight` instead.
