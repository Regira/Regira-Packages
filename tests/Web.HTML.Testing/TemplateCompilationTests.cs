using Regira.Web.HTML.Abstractions;

namespace Web.HTML.Testing;

// Each compiled template loads an assembly that is never unloaded, so the templates' assemblies are counted:
// a parser has to compile a template once, not on every call. Both engines load a template from bytes, so only
// assemblies without a file count — a framework assembly another thread loads meanwhile is not one
[TestFixture]
[NonParallelizable] // a template compiled by a concurrent test would be counted here
public class TemplateCompilationTests
{
    private const string Template = "<p>Hello @Model.Name</p>";
    private const int Calls = 20;
    private const int ConcurrentCalls = 8;
    private static readonly object Model = new { Name = "Alice" };

    private static IEnumerable<TestCaseData> Parsers()
    {
        yield return new TestCaseData(new Func<IHtmlParser>(() => new Regira.Web.HTML.RazorLight.RazorTemplateParser()))
            .SetArgDisplayNames("RazorLight");
        yield return new TestCaseData(new Func<IHtmlParser>(() => new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser()))
            .SetArgDisplayNames("RazorEngineCore");
        yield return new TestCaseData(new Func<IHtmlParser>(() => new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser(new() { HtmlEncode = true })))
            .SetArgDisplayNames("RazorEngineCore HtmlEncode");
    }

    [TestCaseSource(nameof(Parsers))]
    public async Task Repeated_Template_Compiles_Once(Func<IHtmlParser> createParser)
    {
        var parser = createParser();

        Assert.That(await CountAssembliesLoaded(() => parser), Is.Zero);
    }

    [TestCaseSource(nameof(Parsers))]
    public async Task Repeated_Template_Compiles_Once_Across_Parsers(Func<IHtmlParser> createParser)
    {
        Assert.That(await CountAssembliesLoaded(createParser), Is.Zero);
    }

    [TestCaseSource(nameof(Parsers))]
    public async Task Distinct_Templates_Render_Their_Own_Text(Func<IHtmlParser> createParser)
    {
        var parser = createParser();

        Assert.That(await parser.Parse("<p>Hello @Model.Name</p>", Model), Is.EqualTo("<p>Hello Alice</p>"));
        Assert.That(await parser.Parse("<p>Goodbye @Model.Name</p>", Model), Is.EqualTo("<p>Goodbye Alice</p>"));
    }

    [TestCaseSource(nameof(Parsers))]
    public async Task Concurrent_First_Calls_Compile_Once(Func<IHtmlParser> createParser)
    {
        // An earlier template loads what compiling and rendering need, so only the new template's assembly is counted
        await createParser().Parse("<p>Warm-up @Model.Name</p>", Model);
        var id = Guid.NewGuid().ToString("N");
        var template = $"<p>Hello @Model.Name</p><!-- {id} -->";
        var before = LoadedAssemblies();

        using var start = new Barrier(ConcurrentCalls);
        var renders = Enumerable.Range(0, ConcurrentCalls)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                start.SignalAndWait();
                return createParser().Parse(template, Model);
            }, TaskCreationOptions.LongRunning).Unwrap());
        var results = await Task.WhenAll(renders);

        Assert.That(results, Has.All.EqualTo($"<p>Hello Alice</p><!-- {id} -->"));
        Assert.That(TemplatesLoadedSince(before), Is.EqualTo(1));
    }

    // one compiled template serves both modes, each rendering as its own option asks
    [Test]
    public async Task RazorEngineCore_Plain_And_HtmlEncode_Share_One_Compile()
    {
        var model = new { Name = "<b>Alice</b>" };
        var template = $"<p>Hello @Model.Name</p><!-- {Guid.NewGuid():N} -->";
        var plain = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser();
        var encoding = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser(new() { HtmlEncode = true });
        await plain.Parse(Template, model);
        await encoding.Parse(Template, model);
        var before = LoadedAssemblies();

        var plainText = await plain.Parse(template, model);
        var encodedText = await encoding.Parse(template, model);

        Assert.Multiple(() =>
        {
            Assert.That(plainText, Does.StartWith("<p>Hello <b>Alice</b></p>"));
            Assert.That(encodedText, Does.StartWith("<p>Hello &lt;b&gt;Alice&lt;/b&gt;</p>"));
            Assert.That(TemplatesLoadedSince(before), Is.EqualTo(1));
        });
    }

    // a key no longer pins a template: the parser renders the template it is given
    [Test]
    public async Task RazorLight_A_Keyed_Parser_Renders_The_Template_It_Is_Given()
    {
#pragma warning disable CS0618 // TemplateKey is obsolete and ignored
        var parser = new Regira.Web.HTML.RazorLight.RazorTemplateParser(new() { TemplateKey = "invoice-template" });
#pragma warning restore CS0618

        Assert.That(await parser.Parse("<p>Hello @Model.Name</p>", Model), Is.EqualTo("<p>Hello Alice</p>"));
        Assert.That(await parser.Parse("<p>Goodbye @Model.Name</p>", Model), Is.EqualTo("<p>Goodbye Alice</p>"));
    }

    private static async Task<int> CountAssembliesLoaded(Func<IHtmlParser> getParser)
    {
        // The first call compiles the template and loads what rendering needs
        await getParser().Parse(Template, Model);
        var before = LoadedAssemblies();

        for (var i = 0; i < Calls; i++)
        {
            await getParser().Parse(Template, Model);
        }

        return TemplatesLoadedSince(before);
    }

    private static HashSet<System.Reflection.Assembly> LoadedAssemblies() => [.. AppDomain.CurrentDomain.GetAssemblies()];

    private static int TemplatesLoadedSince(HashSet<System.Reflection.Assembly> before)
        => AppDomain.CurrentDomain.GetAssemblies().Count(a => !before.Contains(a) && !a.IsDynamic && string.IsNullOrEmpty(a.Location));
}
