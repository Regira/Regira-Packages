using RazorLight;
using Regira.Web.HTML.Abstractions;
using System.Security.Cryptography;
using System.Text;

namespace Regira.Web.HTML.RazorLight;

// the options are taken for source compatibility; their one member is ignored
#pragma warning disable CS9113
public class RazorTemplateParser(RazorTemplateParser.Options? options = null) : IHtmlParser
#pragma warning restore CS9113
{
    public class Options
    {
        /// <summary>Ignored: every template is cached by its text, so a parser renders the template it is given.</summary>
        [Obsolete("Ignored: every template is cached by its text, so a parser renders the template it is given.")]
        public string? TemplateKey { get; set; }
    }

    // Each compile loads an assembly that is never unloaded. Keyed by their text, templates share one engine, so each
    // distinct template compiles once per process whichever parser instance renders it
    private static readonly RazorLightEngine Engine = new RazorLightEngineBuilder()
        .UseNoProject()
        .Build();

    public Task<string> Parse<T>(string html, T model)
        => Engine.CompileRenderStringAsync(GetTextKey(html), html, model);

    private static string GetTextKey(string html) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(html)));
}