using System.Text.Encodings.Web;
using Regira.Serializing.Newtonsoft.Json;
using Regira.Web.HTML;
using Web.HTML.Testing.Models;

namespace Web.HTML.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class HtmlTemplateParserTests
{
    private const string OrderTemplate = "<h1>{title}</h1>\n<ul>\n<!--{{orderLines}}-->\n<li>{rowNr}. {title}</li>\n<!--{{/orderLines}}-->\n</ul>";

    private static Order CreateOrder(string title, string lineTitle) => new()
    {
        Title = title,
        OrderLines = [new OrderLine { Title = lineTitle, Amount = 2, Price = 10 }, new OrderLine { Title = "Item #2", Amount = 1, Price = 25 }]
    };

    [Test]
    public async Task Tokens_Are_Replaced_By_Serialized_Keys()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer());
        var parsedHtml = await parser.Parse(OrderTemplate, CreateOrder("Order #1", "Item #1"));

        Assert.That(parsedHtml, Is.EqualTo("<h1>Order #1</h1>\n<ul>\n<li>1. Item #1</li><li>2. Item #2</li>\n</ul>"));
    }

    [Test]
    public async Task Token_Without_A_Value_Stays()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer());
        var parsedHtml = await parser.Parse("<p>{title} {id} {missing}</p>", new Order { Title = "Order #1", Id = null });

        Assert.That(parsedHtml, Is.EqualTo("<p>Order #1 {id} {missing}</p>"));
    }

    [Test]
    public async Task Model_Values_Are_Written_Unencoded_By_Default()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer());
        var parsedHtml = await parser.Parse(OrderTemplate, CreateOrder("Order <b>#1</b>", "Tea & <i>biscuits</i>"));

        Assert.That(parsedHtml, Is.EqualTo("<h1>Order <b>#1</b></h1>\n<ul>\n<li>1. Tea & <i>biscuits</i></li><li>2. Item #2</li>\n</ul>"));
    }

    [Test]
    public async Task ValueConverter_Does_Not_Reach_Block_Values()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer(), (_, value) => HtmlEncoder.Default.Encode(value?.ToString() ?? string.Empty));
        var parsedHtml = await parser.Parse(OrderTemplate, CreateOrder("Order <b>#1</b>", "Tea & <i>biscuits</i>"));

        Assert.That(parsedHtml, Is.EqualTo("<h1>Order &lt;b&gt;#1&lt;/b&gt;</h1>\n<ul>\n<li>1. Tea & <i>biscuits</i></li><li>2. Item #2</li>\n</ul>"));
    }

    [Test]
    public async Task HtmlEncode_Encodes_Top_Level_And_Block_Values()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer()) { HtmlEncode = true };
        var parsedHtml = await parser.Parse(OrderTemplate, CreateOrder("Order <b>#1</b>", "Tea & <i>biscuits</i>"));

        Assert.That(parsedHtml, Is.EqualTo("<h1>Order &lt;b&gt;#1&lt;/b&gt;</h1>\n<ul>\n<li>1. Tea &amp; &lt;i&gt;biscuits&lt;/i&gt;</li><li>2. Item #2</li>\n</ul>"));
    }

    [Test]
    public async Task HtmlEncode_Keeps_Number_Formats_In_Blocks()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer()) { HtmlEncode = true };
        var template = "<ul>\n<!--{{orderLines}}-->\n<li>{title}: {price:0.00}</li>\n<!--{{/orderLines}}-->\n</ul>";
        var parsedHtml = await parser.Parse(template, CreateOrder("Order #1", "Tea & biscuits"));

        Assert.That(parsedHtml, Is.EqualTo($"<ul>\n<li>Tea &amp; biscuits: {10m:0.00}</li><li>Item #2: {25m:0.00}</li>\n</ul>"));
    }

    [Test]
    public async Task HtmlEncode_Raw_Writes_Markup()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer()) { HtmlEncode = true };
        var template = "<h1>{title:raw}</h1><p>{title}</p>\n<ul>\n<!--{{orderLines}}-->\n<li>{title:raw}</li>\n<!--{{/orderLines}}-->\n</ul>";
        var parsedHtml = await parser.Parse(template, CreateOrder("Order <b>#1</b>", "Tea & <i>biscuits</i>"));

        Assert.That(parsedHtml, Is.EqualTo("<h1>Order <b>#1</b></h1><p>Order &lt;b&gt;#1&lt;/b&gt;</p>\n<ul>\n<li>Tea & <i>biscuits</i></li><li>Item #2</li>\n</ul>"));
    }

    [Test]
    public async Task HtmlEncode_Block_Value_Cannot_Spell_A_Raw_Token()
    {
        // the block writes its value before the top-level pass fills {title:raw}, so the value must not spell that token
        var parser = new HtmlTemplateParser(new JsonSerializer()) { HtmlEncode = true };
        var parsedHtml = await parser.Parse(OrderTemplate, CreateOrder("<img src=x onerror=alert(1)>", "{title:raw}"));

        Assert.Multiple(() =>
        {
            Assert.That(parsedHtml, Does.Not.Contain("<img"));
            Assert.That(parsedHtml, Does.Contain("<li>1. &#123;title:raw&#125;</li>"));
        });
    }

    [Test]
    public async Task HtmlEncode_Raw_Value_Cannot_Spell_A_Token()
    {
        // a raw value goes in as it is, so its own text must not be read as a token that writes another value raw
        var parser = new HtmlTemplateParser(new JsonSerializer()) { HtmlEncode = true };
        var template = "<i>{title:raw}</i>\n<ul>\n<!--{{lines}}-->\n<li>{name} {name:raw}</li>\n<!--{{/lines}}-->\n</ul><p>{secret}</p>";
        var parsedHtml = await parser.Parse(template, new
        {
            Title = "<b>T</b>{secret:raw}",
            Secret = "<script>s</script>",
            Lines = new[] { new { Name = "{secret:raw}<img>" } }
        });

        Assert.That(parsedHtml, Is.EqualTo("<i><b>T</b>{secret:raw}</i>\n<ul>\n<li>&#123;secret:raw&#125;&lt;img&gt; {secret:raw}<img></li>\n</ul><p>&lt;script&gt;s&lt;/script&gt;</p>"));
    }

    [Test]
    public async Task HtmlEncode_Value_Cannot_Spell_A_Raw_Placeholder()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer()) { HtmlEncode = true };
        var forged = (char)0xE000 + "0" + (char)0xE001;
        var parsedHtml = await parser.Parse("<p>{title}</p><i>{secret:raw}</i>", new { Title = forged, Secret = "<b>S</b>" });

        Assert.That(parsedHtml, Is.EqualTo("<p>&#xE000;0&#xE001;</p><i><b>S</b></i>"));
    }

    [Test]
    public async Task HtmlEncode_Encodes_ValueConverter_Output()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer(), (_, value) => $"<em>{value}</em>") { HtmlEncode = true };
        var parsedHtml = await parser.Parse("<h1>{title}</h1>", new Order { Title = "Order #1" });

        Assert.That(parsedHtml, Is.EqualTo("<h1>&lt;em&gt;Order #1&lt;/em&gt;</h1>"));
    }

    [Test]
    public async Task Raw_Token_Writes_The_Value_When_Encoding_Is_Off()
    {
        var parser = new HtmlTemplateParser(new JsonSerializer());
        var parsedHtml = await parser.Parse("<h1>{title:raw}</h1>", new Order { Title = "Order <b>#1</b>" });

        Assert.That(parsedHtml, Is.EqualTo("<h1>Order <b>#1</b></h1>"));
    }
}
