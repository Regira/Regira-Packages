using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.Paging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// An app-wide repository that has to see every read (logging, auditing) on a complex registration. The generated
/// controllers look a row up by an anonymous object before a save or a delete, so the <c>object</c> overloads have
/// to be overridable on the complex shape as they are on the simple one, and so does the typed <c>Count</c>.
/// </summary>
[TestFixture]
public class AppRepositoryOverrideTests
{
    public class Ticket : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }
    public record TicketSearchObject : SearchObject;
    public enum TicketSortBy { Default }
    [Flags] public enum TicketIncludes { Default = 0, All = 0 }

    public class TicketContext(DbContextOptions<TicketContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
    }

    public class CountingRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(
        IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes> readService,
        IEntityWriteService<TEntity, TKey> writeService)
        : EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>(readService, writeService)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        public static readonly List<string> Calls = [];

        public override Task<long> Count(TSearchObject? so, CancellationToken token = default)
        {
            Calls.Add("Count(TSearchObject)");
            return base.Count(so, token);
        }
        public override Task<long> Count(object? so, CancellationToken token = default)
        {
            Calls.Add("Count(object)");
            return base.Count(so, token);
        }
        public override Task<IList<TEntity>> List(object? so, PagingInfo? pagingInfo, CancellationToken token = default)
        {
            Calls.Add("List(object)");
            return base.List(so, pagingInfo, token);
        }
    }

    public class CountingRepository<TEntity, TSearchObject, TSortBy, TIncludes>(
        IEntityReadService<TEntity, int, TSearchObject, TSortBy, TIncludes> readService,
        IEntityWriteService<TEntity, int> writeService)
        : CountingRepository<TEntity, int, TSearchObject, TSortBy, TIncludes>(readService, writeService),
            IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>
        where TEntity : class, IEntity<int>
        where TSearchObject : class, ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum;

    private SqliteConnection _connection = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        CountingRepository<Ticket, int, TicketSearchObject, TicketSortBy, TicketIncludes>.Calls.Clear();
    }

    [TearDown]
    public void TearDown() => _connection.Close();

    [Test]
    public async Task A_Complex_App_Repository_Sees_The_Lookups_By_An_Anonymous_Object_And_The_Typed_Count()
    {
        var services = new ServiceCollection();
        services.AddDbContext<TicketContext>(db => db.UseSqlite(_connection));
        services.UseEntities<TicketContext>(o => o
                .UseDefaults()
                .UseRepository(typeof(CountingRepository<,,,>), typeof(CountingRepository<,,,,>)))
            .For<Ticket, TicketSearchObject, TicketSortBy, TicketIncludes>();
        await using var sp = services.BuildServiceProvider();
        await sp.GetRequiredService<TicketContext>().Database.EnsureCreatedAsync();

        // the interface the generated controllers resolve for a save check, a delete and the attachment routes
        var service = sp.GetRequiredService<IEntityService<Ticket, int>>();
        await service.Count(new { Id = 1, Archived = ArchivedFilter.Included });
        await service.List(new { Id = 1 }, new PagingInfo { PageSize = 1 });
        // a typed count through the complex interface
        var complex = sp.GetRequiredService<IEntityService<Ticket, TicketSearchObject, TicketSortBy, TicketIncludes>>();
        await complex.Count(new TicketSearchObject());

        Assert.That(CountingRepository<Ticket, int, TicketSearchObject, TicketSortBy, TicketIncludes>.Calls,
            Is.EqualTo(new[] { "Count(object)", "List(object)", "Count(TSearchObject)" }));
    }
}
