using Regira.Web.HTML.Abstractions;

namespace Web.HTML.Testing;

// Each compiled template loads an assembly that is never unloaded, so the loaded assemblies are counted:
// a parser has to compile a template once, not on every call
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
        var before = AppDomain.CurrentDomain.GetAssemblies().Length;

        using var start = new Barrier(ConcurrentCalls);
        var renders = Enumerable.Range(0, ConcurrentCalls)
            .Select(_ => Task.Factory.StartNew(() =>
            {
                start.SignalAndWait();
                return createParser().Parse(template, Model);
            }, TaskCreationOptions.LongRunning).Unwrap());
        var results = await Task.WhenAll(renders);

        Assert.That(results, Has.All.EqualTo($"<p>Hello Alice</p><!-- {id} -->"));
        Assert.That(AppDomain.CurrentDomain.GetAssemblies().Length - before, Is.EqualTo(1));
    }

    [Test]
    public async Task RazorEngineCore_HtmlEncode_Compiles_The_Same_Text_Apart()
    {
        var model = new { Name = "<b>Alice</b>" };
        var plain = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser();
        var encoding = new Regira.Web.HTML.RazorEngineCore.RazorTemplateParser(new() { HtmlEncode = true });

        Assert.That(await plain.Parse(Template, model), Is.EqualTo("<p>Hello <b>Alice</b></p>"));
        Assert.That(await encoding.Parse(Template, model), Is.EqualTo("<p>Hello &lt;b&gt;Alice&lt;/b&gt;</p>"));
    }

    [Test]
    public async Task RazorLight_TemplateKey_Keeps_The_First_Template()
    {
        var parser = new Regira.Web.HTML.RazorLight.RazorTemplateParser(new() { TemplateKey = "invoice-template" });

        Assert.That(await parser.Parse("<p>Hello @Model.Name</p>", Model), Is.EqualTo("<p>Hello Alice</p>"));
        Assert.That(await parser.Parse("<p>Goodbye @Model.Name</p>", Model), Is.EqualTo("<p>Hello Alice</p>"));
    }

    private static async Task<int> CountAssembliesLoaded(Func<IHtmlParser> getParser)
    {
        // The first call compiles the template and loads what rendering needs
        await getParser().Parse(Template, Model);
        var before = AppDomain.CurrentDomain.GetAssemblies().Length;

        for (var i = 0; i < Calls; i++)
        {
            await getParser().Parse(Template, Model);
        }

        return AppDomain.CurrentDomain.GetAssemblies().Length - before;
    }
}
