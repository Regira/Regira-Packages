using Regira.Utilities;
using Regira.Web.HTML.Abstractions;
using Regira.Web.HTML.RazorEngineCore;
using Regira.Web.Utilities;
using Web.HTML.Testing.Models;

namespace Web.HTML.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RazorEngineCoreTemplateTests
{
    private readonly string _assetsDir;
    private readonly IHtmlParser _parser;
    public RazorEngineCoreTemplateTests()
    {
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        _assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        _parser = new RazorTemplateParser();
        Directory.CreateDirectory(Path.Combine(_assetsDir, "Output"));
    }

    [Test]
    public async Task Simple_Razor_Template()
    {
        var inputHtml = await File.ReadAllTextAsync(Path.Combine(_assetsDir, "Input", "simple-razor.cshtml"));
        var model = "test";
        var parser = new RazorTemplateParser();
        var parsedHtml = await parser.Parse(inputHtml, model);

        Assert.That(parsedHtml, Is.Not.Null);
        Assert.That(inputHtml, Is.Not.EqualTo(parsedHtml));
        Assert.That(parsedHtml.Contains($"<p>Hello {model}!</p>"), Is.True);

        await File.WriteAllTextAsync(Path.Combine(_assetsDir, "Output", "simple-razor-engine-core.html"), parsedHtml);
    }

    [Test]
    public async Task Razor_Order_Model()
    {
        var logoBytes = await File.ReadAllBytesAsync(Path.Combine(_assetsDir, "Input", "regira-logo.png"));
        var logoBase64 = UriUtility.ToBase64ImageUrl(logoBytes);
        var inputHtml = await File.ReadAllTextAsync(Path.Combine(_assetsDir, "Input", "razor-order.cshtml"));
        var model = new Order
        {
            Title = "Order #1",
            Created = DateTime.Now,
            ImgBase64 = logoBase64,
            OrderLines =
            [
                new OrderLine{ Title = "Item #1", Amount = 2, Price = 10 },
                new OrderLine{ Title = "Item #2", Amount = 1, Price = 25 },
                new OrderLine{ Title = "Item #3", Amount = 3, Price = 7.5m }
            ]
        };
        var parsedHtml = await _parser.Parse(inputHtml, model);

        Assert.That(parsedHtml, Is.Not.Null);
        Assert.That(inputHtml, Is.Not.EqualTo(parsedHtml));
        Assert.That(parsedHtml.Contains($"<p>Order: {model.Title}</p>"), Is.True);
        foreach (var orderline in model.OrderLines)
        {
            Assert.That(parsedHtml.Contains($"<li>{orderline.Title}: {orderline.Amount} x {orderline.Price}</li>"), Is.True);
        }

        await File.WriteAllTextAsync(Path.Combine(_assetsDir, "Output", "razor-order-engine-core.html"), parsedHtml);
    }

    [Test]
    public async Task Model_Values_Are_Written_Unencoded_By_Default()
    {
        var model = new Order { Title = "Order <b>#1</b>" };
        var parsedHtml = await _parser.Parse("<p>@Model.Title</p>", model);

        Assert.That(parsedHtml, Is.EqualTo("<p>Order <b>#1</b></p>"));
    }

    [Test]
    public async Task HtmlEncode_Encodes_Model_Values()
    {
        var parser = new RazorTemplateParser(new() { HtmlEncode = true });
        var model = new Order { Title = "Order <b>#1</b>" };
        var parsedHtml = await parser.Parse("<p>@Model.Title</p>", model);

        Assert.That(parsedHtml, Is.EqualTo("<p>Order &lt;b&gt;#1&lt;/b&gt;</p>"));
    }

    [Test]
    public async Task HtmlEncode_Raw_Writes_Markup()
    {
        var parser = new RazorTemplateParser(new() { HtmlEncode = true });
        var model = new Order { Title = "Order <b>#1</b>" };
        var parsedHtml = await parser.Parse("<p>@Raw(Model.Title)</p><p title=\"@Raw(Model.Title)\"></p>", model);

        Assert.That(parsedHtml, Is.EqualTo("<p>Order <b>#1</b></p><p title=\"Order <b>#1</b>\"></p>"));
    }

    [Test]
    public async Task HtmlEncode_Encodes_Attribute_Values_But_Not_Template_Text()
    {
        var parser = new RazorTemplateParser(new() { HtmlEncode = true });
        var model = new Order { Id = "1\" onclick=\"alert(1)" };
        var parsedHtml = await parser.Parse("<a href=\"/orders?status=open&amp;id=@Model.Id\">Open</a>", model);

        Assert.That(parsedHtml, Is.EqualTo("<a href=\"/orders?status=open&amp;id=1&quot; onclick=&quot;alert(1)\">Open</a>"));
    }

    [Test]
    public async Task HtmlEncode_Writes_Nothing_For_Null()
    {
        var parser = new RazorTemplateParser(new() { HtmlEncode = true });
        var parsedHtml = await parser.Parse("<p>@Model.Title</p>", new Order());

        Assert.That(parsedHtml, Is.EqualTo("<p></p>"));
    }

    [Test]
    public async Task HtmlEncode_Reads_Anonymous_Model()
    {
        var parser = new RazorTemplateParser(new() { HtmlEncode = true });
        var model = new { Title = "Order <b>#1</b>", Lines = new[] { new { Title = "Tea & biscuits" } } };
        var parsedHtml = await parser.Parse("<p>@Model.Title</p>@foreach (var line in Model.Lines) {<li>@line.Title</li>}", model);

        Assert.That(parsedHtml, Is.EqualTo("<p>Order &lt;b&gt;#1&lt;/b&gt;</p><li>Tea &amp; biscuits</li>"));
    }

    [Test]
    public async Task HtmlEncode_Matches_RazorLight()
    {
        var template = "<p>@Model.Title</p><p>@Raw(Model.Title)</p><a href=\"/orders?status=open&amp;id=@Model.Id\">@Model.Created.Year</a>";
        var model = new Order { Id = "1\" x='y'", Title = "Café <b>#1</b> & +1", Created = new DateTime(2026, 10, 8) };
        var razorLight = new Regira.Web.HTML.RazorLight.RazorTemplateParser();
        var parser = new RazorTemplateParser(new() { HtmlEncode = true });

        var expected = await razorLight.Parse(template, model);
        var parsedHtml = await parser.Parse(template, model);

        Assert.That(parsedHtml, Is.EqualTo(expected));
    }

    [Test]
    public async Task HtmlEncode_Razor_Order_Model()
    {
        var inputHtml = await File.ReadAllTextAsync(Path.Combine(_assetsDir, "Input", "razor-order.cshtml"));
        var model = new Order
        {
            Title = "Order <b>#1</b>",
            Created = DateTime.Now,
            OrderLines =
            [
                new OrderLine{ Title = "Item #1", Amount = 2, Price = 10 },
                new OrderLine{ Title = "Item #2", Amount = 1, Price = 25 }
            ]
        };
        var parser = new RazorTemplateParser(new() { HtmlEncode = true });
        var parsedHtml = await parser.Parse(inputHtml, model);

        Assert.That(parsedHtml, Does.Contain("<p>Order: Order &lt;b&gt;#1&lt;/b&gt;</p>"));
        foreach (var orderline in model.OrderLines)
        {
            Assert.That(parsedHtml, Does.Contain($"<li>{orderline.Title}: {orderline.Amount} x {orderline.Price}</li>"));
        }
    }
}