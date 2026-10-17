using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Regira.Web.HTML.Abstractions;

namespace Regira.Web.HTML.RazorComponents;

/// <summary>
/// Renders a compiled Razor component to HTML with ASP.NET Core's <see cref="HtmlRenderer"/>.
/// <c>@inject</c> resolves from <paramref name="serviceProvider"/>; without one, a component that injects a service fails.
/// </summary>
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
