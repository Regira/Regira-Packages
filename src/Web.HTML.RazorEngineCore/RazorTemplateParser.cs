using RazorEngineCore;
using Regira.Web.HTML.Abstractions;
using System.Text.RegularExpressions;

namespace Regira.Web.HTML.RazorEngineCore;

public class RazorTemplateParser : IHtmlParser
{
    public async Task<string> Parse<T>(string html, T model)
    {
        var engine = new RazorEngine();
        // RazorEngineCore does not support the @model directive; strip it before compiling
        var templateSource = Regex.Replace(html, @"^\s*@model\s+\S+\s*$", string.Empty, RegexOptions.Multiline);
        // RazorEngineCore has no Layout support; strip @{ Layout = null; } blocks
        templateSource = Regex.Replace(templateSource, @"@\{\s*Layout\s*=\s*null\s*;\s*\}", string.Empty);
        var template = await engine.CompileAsync(templateSource);
        return await template.RunAsync(model);
    }
}