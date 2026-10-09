using Microsoft.AspNetCore.Components;

namespace Regira.Web.HTML.Abstractions;

/// <summary>Renders a compiled Razor component to HTML.</summary>
public interface IHtmlComponentRenderer
{
    /// <summary>Renders a Razor component to HTML. Each entry sets the component parameter of that name.</summary>
    Task<string> Render(Type componentType, IDictionary<string, object?>? parameters = null);
}

public static class HtmlComponentRendererExtensions
{
    /// <summary>Renders <typeparamref name="TComponent"/> to HTML. Each entry sets the component parameter of that name.</summary>
    public static Task<string> Render<TComponent>(this IHtmlComponentRenderer renderer, IDictionary<string, object?>? parameters = null)
        where TComponent : IComponent
        => renderer.Render(typeof(TComponent), parameters);

    /// <summary>Renders <typeparamref name="TComponent"/> to HTML with <paramref name="model"/> as its <c>Model</c> parameter.</summary>
    public static Task<string> Render<TComponent, TModel>(this IHtmlComponentRenderer renderer, TModel model)
        where TComponent : IComponent
        => renderer.Render(typeof(TComponent), new Dictionary<string, object?> { ["Model"] = model });
}
