using Entities.Mediator.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Handlers;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Web.Models;

namespace Entities.Mediator.Testing;

/// <summary>What every request goes through, whatever sends it: handler resolution, behaviours and <c>Duration</c>.</summary>
public class EntityRequestExecutorTests
{
    [Fact]
    public async Task UseEntities_Registers_The_InHouse_Sender()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();

        Assert.IsType<EntitySender>(scope.ServiceProvider.GetRequiredService<IEntitySender>());
        Assert.IsType<EntityRequestExecutor>(scope.ServiceProvider.GetRequiredService<IEntityRequestExecutor>());
    }

    [Fact]
    public async Task The_Default_Handler_Answers_And_Null_Means_Not_Found()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEntitySender>();

        var found = await sender.Send(new DetailsQuery<Product, int, Product>(shop.PenId));
        var missing = await sender.Send(new DetailsQuery<Product, int, Product>(shop.PenId + 1));

        Assert.Equal("Pen", found!.Item.Title);
        Assert.NotNull(found.Duration);
        Assert.Null(missing);
    }

    [Fact]
    public async Task A_Closed_Handler_Replaces_The_Default_For_Its_Request_Alone()
    {
        await using var shop = await Shop.CreateServicesAsync(services =>
            services.AddTransient<IEntityRequestHandler<DetailsQuery<Product, int, Product>, DetailsResult<Product>>, FixedDetailsHandler>());
        using var scope = shop.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEntitySender>();

        var details = await sender.Send(new DetailsQuery<Product, int, Product>(shop.PenId));
        var list = await sender.Send(new ListQuery<Product, int, SearchObject<int>, Product>());

        Assert.Equal("Fixed", details!.Item.Title);
        Assert.NotNull(details.Duration);
        Assert.Equal(["Pen"], list!.Items.Select(x => x.Title));
    }

    [Fact]
    public async Task A_Subclassed_Default_Handler_Can_Change_One_Operation()
    {
        await using var shop = await Shop.CreateServicesAsync(services =>
            services.AddTransient<IEntityRequestHandler<DetailsQuery<Product, int, Product>, DetailsResult<Product>>, UpperCaseDetailsHandler>());
        using var scope = shop.Services.CreateScope();

        var details = await scope.ServiceProvider.GetRequiredService<IEntitySender>().Send(new DetailsQuery<Product, int, Product>(shop.PenId));

        Assert.Equal("PEN", details!.Item.Title);
    }

    [Fact]
    public async Task Behaviours_Run_In_Registration_Order_The_First_Outermost()
    {
        var trace = new List<string>();
        await using var shop = await Shop.CreateServicesAsync(services =>
        {
            services.AddSingleton(trace);
            services.AddTransient(typeof(IEntityPipelineBehavior<,>), typeof(OuterBehavior<,>));
            services.AddTransient(typeof(IEntityPipelineBehavior<,>), typeof(InnerBehavior<,>));
        });
        using var scope = shop.Services.CreateScope();

        await scope.ServiceProvider.GetRequiredService<IEntitySender>().Send(new DetailsQuery<Product, int, Product>(shop.PenId));

        Assert.Equal(["outer >", "inner >", "< inner", "< outer"], trace);
    }

    [Fact]
    public async Task Untyped_Execution_Answers_As_Typed_Execution_Does()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();
        var executor = scope.ServiceProvider.GetRequiredService<IEntityRequestExecutor>();

        var response = await executor.Execute((IEntityRequest)new DetailsQuery<Product, int, Product>(shop.PenId));

        var details = Assert.IsType<DetailsResult<Product>>(response);
        Assert.Equal("Pen", details.Item.Title);
        Assert.NotNull(details.Duration);
    }

    [Fact]
    public async Task A_Request_Of_The_Apps_Own_Needs_A_Handler_And_Says_So()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using (var scope = shop.Services.CreateScope())
        {
            var ex = await Assert.ThrowsAsync<InvalidOperationException>(() =>
                scope.ServiceProvider.GetRequiredService<IEntitySender>().Send(new StockQuery(shop.PenId)));
            Assert.Contains("IEntityRequestHandler<StockQuery, Int32>", ex.Message);
        }

        await using var withHandler = await Shop.CreateServicesAsync(services =>
            services.AddTransient<IEntityRequestHandler<StockQuery, int>, StockQueryHandler>());
        using var handled = withHandler.Services.CreateScope();
        Assert.Equal(5, await handled.ServiceProvider.GetRequiredService<IEntitySender>().Send(new StockQuery(withHandler.PenId)));
    }

    [Fact]
    public async Task A_Host_Wired_Without_A_Sender_Falls_Back_To_The_InHouse_One()
    {
        await using var shop = await Shop.CreateServicesAsync(services =>
        {
            services.Remove(services.Single(d => d.ServiceType == typeof(IEntitySender)));
            services.Remove(services.Single(d => d.ServiceType == typeof(IEntityRequestExecutor)));
        });
        using var scope = shop.Services.CreateScope();

        var details = await scope.ServiceProvider.GetEntitySender().Send(new DetailsQuery<Product, int, Product>(shop.PenId));

        Assert.Equal("Pen", details!.Item.Title);
    }

    [Fact]
    public async Task A_Job_Saves_Patches_And_Deletes_With_The_Endpoints_Semantics()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEntitySender>();

        var created = await sender.Send(new SaveCommand<Product, int, Product, ProductInputDto>(new ProductInputDto { Title = "Ink", Stock = 3 }));
        using var patch = System.Text.Json.JsonDocument.Parse("""{ "stock": 7 }""");
        var patched = await sender.Send(new PatchCommand<Product, int, Product, ProductInputDto>(created!.Item.Id, patch.RootElement));
        var deleted = await sender.Send(new DeleteCommand<Product, int, Product>(shop.PenId));

        Assert.True(created.IsNew);
        Assert.Equal(("Ink", 7), (patched!.Item.Title, patched.Item.Stock));
        Assert.Equal(1, deleted!.Affected);
        Assert.Null(await shop.FindAsync(shop.PenId));
    }

    // without SerializerOptions a job's patch may name an enum value or give its number, as a client's JSON would
    [Theory]
    [InlineData("""{ "kind": "Refill" }""")]
    [InlineData("""{ "kind": "refill" }""")]
    [InlineData("""{ "kind": 1 }""")]
    public async Task A_Patch_Without_Options_Reads_An_Enum_By_Name_Or_Number(string body)
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEntitySender>();

        using var patch = System.Text.Json.JsonDocument.Parse(body);
        var patched = await sender.Send(new PatchCommand<Product, int, Product, ProductInputDto>(shop.PenId, patch.RootElement));

        Assert.Equal(ProductKind.Refill, patched!.Item.Kind);
        Assert.Equal("Pen", patched.Item.Title);
    }

    [Fact]
    public async Task A_Search_Counts_And_Pages()
    {
        await using var shop = await Shop.CreateServicesAsync();
        using var scope = shop.Services.CreateScope();
        var sender = scope.ServiceProvider.GetRequiredService<IEntitySender>();
        await sender.Send(new SaveCommand<Product, int, Product, ProductInputDto>(new ProductInputDto { Title = "Ink", Stock = 3 }));

        var page = await sender.Send(new SearchQuery<Product, int, SearchObject<int>, Product>(null, new Regira.DAL.Paging.PagingInfo { PageSize = 1 }));

        Assert.Equal(2, page!.Count);
        Assert.Single(page.Items);
    }

    public sealed class FixedDetailsHandler : IEntityRequestHandler<DetailsQuery<Product, int, Product>, DetailsResult<Product>>
    {
        public Task<DetailsResult<Product>?> Handle(DetailsQuery<Product, int, Product> request, CancellationToken token = default)
            => Task.FromResult<DetailsResult<Product>?>(new DetailsResult<Product> { Item = new Product { Id = request.Id, Title = "Fixed" } });
    }

    public sealed class UpperCaseDetailsHandler(IServiceProvider services) : DetailsHandler<Product, int, Product>(services)
    {
        public override async Task<DetailsResult<Product>?> Handle(DetailsQuery<Product, int, Product> request, CancellationToken token = default)
        {
            var result = await base.Handle(request, token);
            if (result != null)
            {
                result.Item.Title = result.Item.Title.ToUpperInvariant();
            }
            return result;
        }
    }

    public sealed class OuterBehavior<TRequest, TResponse>(List<string> trace) : IEntityPipelineBehavior<TRequest, TResponse>
        where TRequest : IEntityRequest<TResponse>
    {
        public async Task<TResponse?> Handle(TRequest request, EntityRequestDelegate<TResponse> next, CancellationToken token = default)
        {
            trace.Add("outer >");
            var response = await next();
            trace.Add("< outer");
            return response;
        }
    }

    public sealed class InnerBehavior<TRequest, TResponse>(List<string> trace) : IEntityPipelineBehavior<TRequest, TResponse>
        where TRequest : IEntityRequest<TResponse>
    {
        public async Task<TResponse?> Handle(TRequest request, EntityRequestDelegate<TResponse> next, CancellationToken token = default)
        {
            trace.Add("inner >");
            var response = await next();
            trace.Add("< inner");
            return response;
        }
    }

    public sealed record StockQuery(int Id) : IEntityRequest<int>
    {
        public Type EntityType => typeof(Product);
        public EntityOperation Operation => EntityOperation.Details;
    }

    public sealed class StockQueryHandler(ShopContext db) : IEntityRequestHandler<StockQuery, int>
    {
        public async Task<int> Handle(StockQuery request, CancellationToken token = default)
            => (await db.Products.FindAsync([request.Id], token))!.Stock;
    }
}
