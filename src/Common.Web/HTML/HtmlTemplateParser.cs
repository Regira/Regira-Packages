using System.Text.RegularExpressions;
using Regira.Serializing.Abstractions;
using Regira.Utilities;
using Regira.Web.HTML.Abstractions;

namespace Regira.Web.HTML;

public class HtmlTemplateParser : IHtmlParser
{
    private readonly ISerializer _serializer;
    public Func<string, object?, string> ValueConverter { get; set; }

    public HtmlTemplateParser(ISerializer serializer, Func<string, object?, string>? valueConverter = null)
    {
        _serializer = serializer;
        ValueConverter = valueConverter ?? ((_, value) => value?.ToString() ?? string.Empty);
    }


    public Task<string> Parse<T>(string html, T model)
    {
        var flatModel = GetFlatDictionary(model);
        var content = ProcessInput(html, flatModel);
        return Task.FromResult(content);
    }


    protected IDictionary<string, object?>? GetFlatDictionary<T>(T model)
    {
        if (model == null)
        {
            return null;
        }
        var json = _serializer.Serialize(model);
        var dic = _serializer.Deserialize<Dictionary<string, object?>>(json)!;
        return DictionaryUtility.Flatten(dic);
    }
    protected string ProcessInput(string htmlContent, IDictionary<string, object?>? paramObj)
    {
        var blockMatches = Regex.Matches(htmlContent, @"<!--{{([a-z][a-z|A-Z]*)}}-->[\s\S]+?<!--{{\/([a-z][a-z|A-Z]*)}}-->");
        var parameters = paramObj?.ToList() ?? [];

        foreach (var blockMatch in blockMatches)
        {
            var lines = blockMatch.ToString()!.Split(Environment.NewLine.ToCharArray());
            var blockName = lines[0].Substring("<!--{{".Length, lines[0].IndexOf("}}-->", StringComparison.Ordinal) - "<!--{{".Length);
            var blockContent = string.Join(Environment.NewLine, lines.Skip(1).Take(lines.Length - 2)).Trim()
                .Replace(Environment.NewLine + Environment.NewLine, Environment.NewLine);
            var groupedBlockParameters = parameters.FindAll(p => p.Key.StartsWith(blockName + "."))
                .Select(p => new { Key = string.Join(".", p.Key.Split('.').Skip(1)), p.Value })
                .GroupBy(p => p.Key.Split('.').First());
            var blockParameters = groupedBlockParameters
                .ToDictionary(g => int.Parse(g.Key), g => g.ToDictionary(x => string.Join(".", x.Key.Split('.').Skip(1)), x => x.Value));
            string result = string.Empty;
            for (var i = 0; i < blockParameters.Count; i++)
            {
                var item = blockParameters[i];
                if (!item.ContainsKey("rowNr"))
                {
                    item["rowNr"] = i + 1;
                }
                var parameterContent = blockContent.Inject(item);
                result += parameterContent;
            }
            htmlContent = htmlContent.Replace(blockMatch.ToString()!, result);
            parameters.RemoveAll(x => x.Key.StartsWith(blockName + "."));
        }
        var paramsDic = parameters.ToDictionary(k => k.Key, v => ValueConverter(v.Key, v.Value));

        return htmlContent.Inject(paramsDic)!;
    }
}