using Mapster;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mapping.Mapster;
using Regira.Entities.Models.Abstractions;

namespace Entities.Mapping.Mapster.Testing;

/// <summary>
/// <c>UseMapping&lt;,&gt;()</c> and <c>AddMapping&lt;,&gt;()</c> add their pairs to the shared TypeAdapterConfig, so a
/// config that requires explicit mapping maps them (SharedConfigTests: a rule the app set for a pair stays).
/// </summary>
[TestFixture]
public class ExplicitMappingTests
{
    public class Order : IEntity<int> { public int Id { get; set; } public string? Title { get; set; } public List<OrderItem>? Items { get; set; } }
    public class OrderItem { public int Id { get; set; } public string? Product { get; set; } }
    public class OrderDto { public int Id { get; set; } public string? Title { get; set; } public List<OrderItemDto>? Items { get; set; } }
    public class OrderItemDto { public int Id { get; set; } public string? Product { get; set; } }
    public class OrderInputDto { public string? Title { get; set; } }

    public class OrderContext(DbContextOptions<OrderContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
    }

    [Test]
    public void A_Config_That_Requires_Explicit_Mapping_Maps_The_Registered_Pairs()
    {
        var services = new ServiceCollection();
        services.AddDbContext<OrderContext>(o => o.UseSqlite("Filename=:memory:"));
        services.UseEntities<OrderContext>(o => o.UseMapsterMapping(cfg => cfg.RequireExplicitMapping = true))
            .For<Order>(e =>
            {
                e.UseMapping<OrderDto, OrderInputDto>();
                e.AddMapping<OrderItem, OrderItemDto>();
            });
        var mapper = services.BuildServiceProvider().GetRequiredService<IEntityMapper>();

        var dto = mapper.Map<OrderDto>(new Order { Id = 7, Title = "order", Items = [new OrderItem { Id = 1, Product = "desk" }] });
        var entity = mapper.Map<Order>(new OrderInputDto { Title = "input" });

        Assert.Multiple(() =>
        {
            Assert.That((dto.Id, dto.Title), Is.EqualTo((7, "order")));
            Assert.That(dto.Items!.Select(i => (i.Id, i.Product)), Is.EqualTo(new[] { (1, "desk") }));
            Assert.That(entity.Title, Is.EqualTo("input"));
        });
    }
}
