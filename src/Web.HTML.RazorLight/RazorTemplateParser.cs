using RazorLight;
using Regira.Web.HTML.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace Regira.Web.HTML.RazorLight;

public class RazorTemplateParser(RazorTemplateParser.Options? options = null) : IHtmlParser
{
    public class Options
    {
        /// <summary>
        /// Caches the first template this parser compiles under this key and renders that template on every later call,
        /// whatever template text is passed. Not set by default: templates are cached by their text.
        /// </summary>
        public string? TemplateKey { get; set; }
    }

    // Each compile loads an assembly that is never unloaded. Keyed by their text, templates can share one engine,
    // so each distinct template compiles once per process whichever parser instance renders it
    private static readonly RazorLightEngine SharedEngine = CreateEngine();
    // Only a parser with a TemplateKey has an engine of its own
    private readonly Lazy<RazorLightEngine> _engine = new(CreateEngine);

    public async Task<string> Parse<T>(string html, T model)
    {
        var templateKey = options?.TemplateKey;
        if (templateKey == null)
        {
            return await SharedEngine.CompileRenderStringAsync(GetTextKey(html), html, model);
        }

        return await _engine.Value.CompileRenderStringAsync(templateKey, html, model);
    }

    private static RazorLightEngine CreateEngine() => new RazorLightEngineBuilder()
        .UseNoProject()
        .Build();

    private static string GetTextKey(string html) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)));
}