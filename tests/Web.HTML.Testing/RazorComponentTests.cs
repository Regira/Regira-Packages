using Microsoft.Extensions.DependencyInjection;
using Regira.Utilities;
using Regira.Web.HTML.Abstractions;
using Regira.Web.HTML.RazorComponents;
using Regira.Web.Utilities;
using Web.HTML.Testing.Models;
using Web.HTML.Testing.Templates;

namespace Web.HTML.Testing;

[TestFixture]
[Parallelizable(ParallelScope.All)]
public class RazorComponentTests
{
    private readonly string _assetsDir;
    private readonly IHtmlComponentRenderer _renderer;
    public RazorComponentTests()
    {
        var assemblyDir = AssemblyUtility.GetAssemblyDirectory()!;
        _assetsDir = Path.Combine(assemblyDir, "../../../", "Assets");
        _renderer = new RazorComponentRenderer();
        Directory.CreateDirectory(Path.Combine(_assetsDir, "Output"));
    }

    [Test]
    public async Task Simple_Razor_Template()
    {
        var model = "test";
        var renderedHtml = await _renderer.Render<SimpleRazor, string>(model);

        Assert.That(renderedHtml, Does.Contain($"<p>Hello {model}!</p>"));

        await File.WriteAllTextAsync(Path.Combine(_assetsDir, "Output", "simple-razor-components.html"), renderedHtml);
    }

    [Test]
    public async Task Razor_Order_Model()
    {
        var logoBytes = await File.ReadAllBytesAsync(Path.Combine(_assetsDir, "Input", "regira-logo.png"));
        var logoBase64 = UriUtility.ToBase64ImageUrl(logoBytes);
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
        var renderedHtml = await _renderer.Render<RazorOrder, Order>(model);

        Assert.That(renderedHtml, Does.Contain($"<p>Order: {model.Title}</p>"));
        foreach (var orderline in model.OrderLines)
        {
            Assert.That(renderedHtml, Does.Contain($"<li>{orderline.Title}: {orderline.Amount} x {orderline.Price}</li>"));
        }

        await File.WriteAllTextAsync(Path.Combine(_assetsDir, "Output", "razor-order-components.html"), renderedHtml);
    }

    [Test]
    public async Task Child_And_Generic_Components_Render()
    {
        var model = new Order
        {
            OrderLines =
            [
                new OrderLine{ Title = "Item #1", Amount = 2, Price = 10 },
                new OrderLine{ Title = "Item #2", Amount = 1, Price = 25 }
            ]
        };
        var renderedHtml = await _renderer.Render<OrderLines, Order>(model);

        Assert.That(renderedHtml, Is.EqualTo("<ul><li>Item #1: 2 x 10</li><li>Item #2: 1 x 25</li></ul>"));
    }

    [Test]
    public async Task Model_Values_Are_Html_Encoded()
    {
        var model = new Order { Title = "Order <b>#1</b>" };
        var renderedHtml = await _renderer.Render<OrderTitle, Order>(model);

        Assert.That(renderedHtml, Is.EqualTo("<p>Order &lt;b&gt;#1&lt;/b&gt;</p>"));
    }

    [Test]
    public async Task MarkupString_Writes_Markup()
    {
        var model = new Order { Title = "Order <b>#1</b>" };
        var renderedHtml = await _renderer.Render<OrderTitleMarkup, Order>(model);

        Assert.That(renderedHtml, Is.EqualTo("<p>Order <b>#1</b></p>"));
    }

    [Test]
    public async Task Async_Lifecycle_Completes_Before_Html_Is_Returned()
    {
        var renderedHtml = await _renderer.Render<AsyncOrder>();

        Assert.That(renderedHtml, Is.EqualTo("<p>Loaded</p>"));
    }

    [Test]
    public async Task Inject_Resolves_From_The_Service_Provider()
    {
        var services = new ServiceCollection()
            .AddSingleton<OrderNumberFormatter>()
            .AddTransient<IHtmlComponentRenderer, RazorComponentRenderer>()
            .BuildServiceProvider();
        var renderer = services.GetRequiredService<IHtmlComponentRenderer>();
        var parameters = new Dictionary<string, object?> { ["Model"] = new Order { Id = "1" } };

        var renderedHtml = await renderer.Render(typeof(OrderNumber), parameters);

        Assert.That(renderedHtml, Is.EqualTo("<p>ORD-1</p>"));
    }

    [Test]
    public async Task Inject_Without_A_Registered_Service_Throws()
    {
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _renderer.Render<OrderNumber, Order>(new Order { Id = "1" }));

        Assert.That(ex!.Message, Does.Contain(nameof(OrderNumberFormatter)));
    }

    [Test]
    public async Task Unknown_Parameter_Throws()
    {
        var parameters = new Dictionary<string, object?> { ["Order"] = new Order() };

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => _renderer.Render<OrderTitle>(parameters));

        Assert.That(ex!.Message, Does.Contain("'Order'"));
    }

    [Test]
    public async Task Template_Exception_Propagates_Unchanged()
    {
        var ex = await Assert.ThrowsAsync<ArgumentException>(() => _renderer.Render<FailingOrder, Order>(new Order { Title = "Order #1" }));

        Assert.That(ex!.Message, Does.StartWith("An order needs at least one order line."));
    }
}
