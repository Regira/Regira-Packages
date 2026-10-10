using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// The single-type-argument <c>Related&lt;TRelated&gt;</c> for an int-keyed child is on every builder, the plain
/// <c>For&lt;TEntity, TKey&gt;()</c> one included: a Guid-keyed parent registers its int-keyed children — nested
/// ones too — without naming the key type, and a second lambda binds as <c>configure</c> or <c>prepareFunc</c>
/// by what its body does.
/// </summary>
[TestFixture]
public class RelatedIntShortcutTests
{
    public class Order : IEntity<Guid>
    {
        public Guid Id { get; set; }
        public string? Title { get; set; }
        public int LineCount { get; set; }
        public ICollection<OrderLine>? Lines { get; set; }
    }
    public class OrderLine : IEntity<int>
    {
        public int Id { get; set; }
        public Guid OrderId { get; set; }
        public string? Product { get; set; }
        public ICollection<OrderLineNote>? Notes { get; set; }
    }
    public class OrderLineNote : IEntity<int>
    {
        public int Id { get; set; }
        public int OrderLineId { get; set; }
        public string? Text { get; set; }
    }
    public class OrderContext(DbContextOptions<OrderContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
    }

    private SqliteConnection _connection = null!;
    [SetUp]
    public void Setup() { _connection = new SqliteConnection("Filename=:memory:"); _connection.Open(); }
    [TearDown]
    public void TearDown() => _connection.Close();

    private async Task<ServiceProvider> Build(Action<Regira.Entities.DependencyInjection.ServiceBuilders.EntityServiceBuilder<OrderContext, Order, Guid>> configure)
    {
        var sp = new ServiceCollection()
            .AddDbContext<OrderContext>(db => db.UseSqlite(_connection))
            .UseEntities<OrderContext>()
            .For<Order, Guid>(configure)
            .Services
            .BuildServiceProvider();
        await sp.GetRequiredService<OrderContext>().Database.EnsureCreatedAsync();
        return sp;
    }

    private static Order NewOrder() => new()
    {
        Title = "Order",
        Lines = [new OrderLine { Product = "Desk", Notes = [new OrderLineNote { Text = "oak" }] }, new OrderLine { Product = "Chair" }],
    };

    [Test]
    public async Task A_Guid_Parent_Registers_Nested_Int_Children_Without_Key_Type_Arguments()
    {
        var sp = await Build(e => e.Related(x => x.Lines, lines => lines.Related(x => x.Notes)));

        var order = NewOrder();
        using (var scope = sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order, Guid>>();
            await service.Add(order);
            await service.SaveChanges();
        }

        using var readScope = sp.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<OrderContext>().Orders
            .Include(x => x.Lines!).ThenInclude(x => x.Notes)
            .SingleAsync(x => x.Id == order.Id);
        Assert.Multiple(() =>
        {
            Assert.That(stored.Lines!.Select(x => x.Product), Is.EquivalentTo(new[] { "Desk", "Chair" }));
            Assert.That(stored.Lines!.Single(x => x.Product == "Desk").Notes!.Select(x => x.Text), Is.EqualTo(new[] { "oak" }));
        });
    }

    public record OrderSearchObject : SearchObject<Guid>;
    public enum OrderSortBy { Default, Title }
    [Flags] public enum OrderIncludes { Default = 0, Lines = 1 }

    [Test]
    public async Task A_Complex_Guid_Parent_Keeps_Its_Builder_Type_After_Related()
    {
        // Related returns the complex builder, so the typed SortBy / Includes chain on after it
        var sp = new ServiceCollection()
            .AddDbContext<OrderContext>(db => db.UseSqlite(_connection))
            .UseEntities<OrderContext>()
            .For<Order, Guid, OrderSearchObject, OrderSortBy, OrderIncludes>(e => e
                .Related(x => x.Lines, lines => lines.Related(x => x.Notes))
                .SortBy((query, sortBy) => sortBy == OrderSortBy.Title ? query.OrderBy(x => x.Title) : query)
                .Includes((query, includes) => includes?.HasFlag(OrderIncludes.Lines) == true ? query.Include(x => x.Lines) : query))
            .Services
            .BuildServiceProvider();
        await sp.GetRequiredService<OrderContext>().Database.EnsureCreatedAsync();

        var order = NewOrder();
        using (var scope = sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order, Guid>>();
            await service.Add(order);
            await service.SaveChanges();
        }

        using var readScope = sp.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<OrderContext>().Orders
            .Include(x => x.Lines!).ThenInclude(x => x.Notes)
            .SingleAsync(x => x.Id == order.Id);
        Assert.That(stored.Lines!.SelectMany(x => x.Notes ?? []).Select(x => x.Text), Is.EqualTo(new[] { "oak" }));
    }

    [Test]
    public async Task A_Second_Lambda_On_The_Parent_Binds_As_Its_Prepare_Function()
    {
        var sp = await Build(e => e.Related(x => x.Lines, o => o.LineCount = o.Lines?.Count ?? 0));

        var order = NewOrder();
        using (var scope = sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Order, Guid>>();
            await service.Add(order);
            await service.SaveChanges();
        }

        using var readScope = sp.CreateScope();
        var stored = await readScope.ServiceProvider.GetRequiredService<OrderContext>().Orders.SingleAsync(x => x.Id == order.Id);
        Assert.That(stored.LineCount, Is.EqualTo(2));
    }
}
