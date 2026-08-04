using RazorLight;
using Regira.Web.HTML.Abstractions;

namespace Regira.Web.HTML.RazorLight;

public class RazorTemplateParser(RazorTemplateParser.Options? options = null) : IHtmlParser
{
    public class Options
    {
        public string? TemplateKey { get; set; }
    }

    private readonly RazorLightEngine _engine = new RazorLightEngineBuilder()
        .UseNoProject()
        .Build();

    public async Task<string> Parse<T>(string html, T model)
    {
        var templateKey = options?.TemplateKey ?? Guid.NewGuid().ToString();

        return await _engine.CompileRenderStringAsync(templateKey, html, model);
    }
}