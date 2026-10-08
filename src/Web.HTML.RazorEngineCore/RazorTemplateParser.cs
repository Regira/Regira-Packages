using RazorEngineCore;
using Regira.Web.HTML.Abstractions;
using System.Collections.Concurrent;
using System.Text.RegularExpressions;

namespace Regira.Web.HTML.RazorEngineCore;

public class RazorTemplateParser(RazorTemplateParser.Options? options) : IHtmlParser
{
    public class Options
    {
        /// <summary>
        /// HTML-encodes every value the template writes with <c>@</c>, as RazorLight does; <c>@Raw(value)</c> writes a value unencoded.
        /// Off by default: values are written as they are.
        /// </summary>
        public bool HtmlEncode { get; set; }
    }

    // Each compile loads an assembly that is never unloaded: compile each distinct template text once per process.
    // GetOrAdd can run its factory on several threads at once, so it only creates the Lazy and one Lazy compiles
    private static readonly ConcurrentDictionary<string, Lazy<Task<IRazorEngineCompiledTemplate>>> Templates = new();
    private static readonly ConcurrentDictionary<string, Lazy<Task<IRazorEngineCompiledTemplate<HtmlEncodingTemplateBase>>>> EncodingTemplates = new();

    public RazorTemplateParser()
        : this(null)
    {
    }

    public async Task<string> Parse<T>(string html, T model)
    {
        if (options?.HtmlEncode == true)
        {
            var encodingTemplate = await EncodingTemplates.GetOrAdd(html, text => new(() => new RazorEngine().CompileAsync<HtmlEncodingTemplateBase>(ToTemplateSource(text)))).Value;
            // the typed run leaves an anonymous model as it is, and its members are internal to the caller's assembly
            object? templateModel = model != null && model.IsAnonymous() ? new AnonymousTypeWrapper(model) : model;
            return await encodingTemplate.RunAsync(instance => instance.Model = templateModel);
        }

        var template = await Templates.GetOrAdd(html, text => new(() => new RazorEngine().CompileAsync(ToTemplateSource(text)))).Value;
        return await template.RunAsync(model);
    }

    private static string ToTemplateSource(string html)
    {
        // RazorEngineCore does not support the @model directive; strip it before compiling
        var templateSource = Regex.Replace(html, @"^\s*@model\s+\S+\s*$", string.Empty, RegexOptions.Multiline);
        // RazorEngineCore has no Layout support; strip @{ Layout = null; } blocks
        return Regex.Replace(templateSource, @"@\{\s*Layout\s*=\s*null\s*;\s*\}", string.Empty);
    }
}
