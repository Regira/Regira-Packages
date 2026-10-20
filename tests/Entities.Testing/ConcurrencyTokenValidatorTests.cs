using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attributes;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.ServiceBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.Testing;

/// <summary>
/// The startup check for a concurrency token that is never checked, never moves, or cannot make the round trip to
/// the client. The write path compares a version stamp with the value the client sent, so a DTO without the property
/// silently turns every write into last-write-wins.
/// </summary>
[TestFixture]
public class ConcurrencyTokenValidatorTests
{
    private const string Hazard = "concurrency token";

    /// An application-owned version stamp, declared so.
    public class Order : IEntity<int>
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        [ConcurrencyCheck, VersionStamp] public Guid Version { get; set; }
    }

    /// A stamp the client must send: an input DTO that cannot carry it refuses every update.
    public class Booking : IEntity<int>
    {
        public int Id { get; set; }
        [ConcurrencyCheck, VersionStamp(Required = true)] public Guid Version { get; set; }
    }

    /// A token on a data column, edited by clients — never a stamp.
    public class Shipment : IEntity<int>
    {
        public int Id { get; set; }
        [ConcurrencyCheck] public string Status { get; set; } = "New";
    }

    /// A token shaped like a stamp that nobody declared one: compared as a data column.
    public class Voucher : IEntity<int>
    {
        public int Id { get; set; }
        [ConcurrencyCheck] public Guid Version { get; set; }
    }

    /// A timestamp token the LastModified primer moves, undeclared.
    public class Ledger : IEntity<int>, IHasLastModified
    {
        public int Id { get; set; }
        [ConcurrencyCheck] public DateTime? LastModified { get; set; }
    }

    /// An owner whose Related() children carry an undeclared token of their own.
    public class Tour : IEntity<int>
    {
        public int Id { get; set; }
        public ICollection<Stop>? Stops { get; set; }
    }

    public class Stop : IEntity<int>
    {
        public int Id { get; set; }
        public int TourId { get; set; }
        [ConcurrencyCheck] public Guid Version { get; set; }
    }

    /// A stamp declaration on a property the model never made a token.
    public class Parcel : IEntity<int>
    {
        public int Id { get; set; }
        [VersionStamp] public Guid Version { get; set; }
    }

    /// The marker: declared a token by the wiring, moved by the primer.
    public class Basket : IEntity<int>, IHasConcurrencyToken
    {
        public int Id { get; set; }
        public Guid ConcurrencyToken { get; set; }
    }

    /// The marker with its token kept out of the model: a convention cannot declare a property that is not mapped.
    public class Ticket : IEntity<int>, IHasConcurrencyToken
    {
        public int Id { get; set; }
        [NotMapped] public Guid ConcurrencyToken { get; set; }
    }

    /// No token at all — the negative control.
    public class Note : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    public record OrderDto
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        public Guid Version { get; set; }
    }

    public record OrderInputDto
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        public Guid Version { get; set; }
    }

    /// The hazard: a DTO without the token.
    public record BareDto
    {
        public int Id { get; set; }
        public string? Status { get; set; }
    }

    public record IdOnlyDto
    {
        public int Id { get; set; }
    }

    /// A consumer's own variant of the built-in primer — it mints, so it must count.
    public class RotatingTokenPrimer : HasConcurrencyTokenDbPrimer;

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Booking> Bookings => Set<Booking>();
        public DbSet<Basket> Baskets => Set<Basket>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<Shipment> Shipments => Set<Shipment>();
        public DbSet<Voucher> Vouchers => Set<Voucher>();
        public DbSet<Parcel> Parcels => Set<Parcel>();
        public DbSet<Ledger> Ledgers => Set<Ledger>();
        public DbSet<Tour> Tours => Set<Tour>();
        public DbSet<Stop> Stops => Set<Stop>();
    }

    private SqliteConnection _connection = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }

    [TearDown]
    public void TearDown() => _connection.Close();

    /// <summary>
    /// The startup issues about concurrency tokens, for one entity registered with the given mapping — warnings and
    /// errors, or from <paramref name="minimum"/> up, whose message contains <paramref name="about"/>. The defaults are
    /// <c>UseDefaults()</c>; <paramref name="configure"/> replaces them, and <paramref name="register"/> adds services
    /// of its own.
    /// </summary>
    private async Task<List<(LogLevel Level, string Message)>> Issues<TEntity>(EntityMappingRegistration? mapping,
        Action<EntityServiceCollectionOptions>? configure = null, Action<IServiceCollection>? register = null,
        LogLevel minimum = LogLevel.Warning, string about = Hazard, Action<EntityIntServiceBuilder<ShopContext, TEntity>>? entity = null)
        where TEntity : class, IEntity<int>
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ShopContext>(o =>
            {
                (configure ?? (x => x.UseDefaults()))(o);
                o.ConfigureValidation(v =>
                {
                    v.Enabled = true;
                    v.ThrowOnError = false;
                });
            })
            .For<TEntity>(e => entity?.Invoke(e));
        if (mapping != null)
        {
            // what UseMapping<TDto, TInputDto>() records — registered directly to keep the fixture mapper-agnostic;
            // the validator reads the descriptor, not the mapper
            services.AddSingleton(mapping);
        }
        register?.Invoke(services);

        await using var sp = services.BuildServiceProvider();
        foreach (var hostedService in sp.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
        return capture.Entries.Where(e => e.Level >= minimum && e.Message.Contains(about)).ToList();
    }

    // ── the hazards ────────────────────────────────────────────────────────────

    [Test]
    public async Task A_Token_The_Input_Dto_Cannot_Carry_Is_Reported()
    {
        var issues = await Issues<Order>(new EntityMappingRegistration(typeof(Order), typeof(OrderDto), typeof(BareDto)));

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(issues[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(issues[0].Message, Does.Contain("Order.Version").And.Contain(nameof(BareDto)));
            Assert.That(issues[0].Message, Does.Contain("last-write-wins"), "the message must carry the symptom");
            Assert.That(issues[0].Message, Does.Contain("public Guid Version { get; set; }"), "the message must carry the remedy");
        });
    }

    /// <summary>What Regira.Entities.Web contributes from the entity controllers' generic arguments.</summary>
    private sealed class DeclaredShapes(params EntityMappingRegistration[] shapes) : IEntityDtoShapeSource
    {
        public IEnumerable<EntityMappingRegistration> GetDtoShapes() => shapes;
    }

    private sealed class ThrowingShapes : IEntityDtoShapeSource
    {
        public IEnumerable<EntityMappingRegistration> GetDtoShapes() => throw new InvalidOperationException("broken application part");
    }

    [Test]
    public async Task A_Shape_Source_That_Throws_Is_Reported_Not_Swallowed()
    {
        // the check cannot judge the DTOs, and the validation run says so rather than passing silently
        var issues = await Issues<Order>(null, register: s => s.AddSingleton<IEntityDtoShapeSource>(new ThrowingShapes()),
            about: "ConcurrencyTokenValidator");

        Assert.That(issues, Has.Some.Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains("could not complete and was skipped") && i.Message.Contains("broken application part")));
    }

    [Test]
    public async Task A_Token_A_Controllers_Input_Dto_Cannot_Carry_Is_Reported_Without_UseMapping()
    {
        // The documented default declares the DTOs on the controller alone; the check used to see only UseMapping
        // registrations and so stayed silent while every PUT went last-write-wins.
        var issues = await Issues<Order>(null, register: s =>
            s.AddSingleton<IEntityDtoShapeSource>(new DeclaredShapes(new EntityMappingRegistration(typeof(Order), typeof(OrderDto), typeof(BareDto)))));

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.That(issues[0].Message, Does.Contain("Order.Version").And.Contain(nameof(BareDto)));
    }

    [Test]
    public async Task A_UseMapping_Registration_Wins_Over_A_Declared_Shape()
    {
        var issues = await Issues<Order>(new EntityMappingRegistration(typeof(Order), typeof(OrderDto), typeof(OrderInputDto)), register: s =>
            s.AddSingleton<IEntityDtoShapeSource>(new DeclaredShapes(new EntityMappingRegistration(typeof(Order), typeof(OrderDto), typeof(BareDto)))));

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task A_Required_Stamp_The_Input_Dto_Cannot_Carry_Is_An_Error()
    {
        // the write path refuses every update without the stamp, so the missing property is not a silent overwrite
        // but a controller that answers 400 to every PUT and PATCH
        var issues = await Issues<Booking>(new EntityMappingRegistration(typeof(Booking), typeof(OrderDto), typeof(BareDto)));

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(issues[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(issues[0].Message, Does.Contain("Booking.Version").And.Contain(nameof(BareDto)).And.Contain("answers 400"));
        });
    }

    [Test]
    public async Task A_Token_The_Read_Dto_Does_Not_Return_Is_Reported()
    {
        // the client can send a token back only if it received one
        var issues = await Issues<Order>(new EntityMappingRegistration(typeof(Order), typeof(BareDto), typeof(OrderInputDto)));

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains(nameof(BareDto))));
    }

    [Test]
    public async Task A_Marker_The_Wiring_Never_Declared_Is_An_Error()
    {
        var issues = await Issues<Basket>(null, o =>
        {
            o.UseDefaults();
            o.WireDbContext(DbContextWiring.All & ~DbContextWiring.ConcurrencyTokens);
        });

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(issues[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(issues[0].Message, Does.Contain(nameof(Basket)).And.Contain("DbContextWiring.ConcurrencyTokens"));
        });
    }

    [Test]
    public async Task A_Marker_Whose_Token_The_Model_Never_Got_Is_An_Error()
    {
        // the wiring ran, so the context is convention-wired — but [NotMapped] keeps the property out of the model,
        // and a token nothing compares is the silent last-write-wins this check exists to catch
        var issues = await Issues<Ticket>(null, o => o.UseDefaults());

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Error && i.Message.Contains(nameof(Ticket)) && i.Message.Contains("[NotMapped]")));
    }

    [Test]
    public async Task A_Marker_Nothing_Mints_Is_Reported()
    {
        // the wiring without the default primers: the property is a token, but no save ever moves it
        var issues = await Issues<Basket>(null, o => o.WireDbContext(DbContextWiring.All));

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains("HasConcurrencyTokenDbPrimer is not registered")));
    }

    [Test]
    public async Task A_Version_Stamp_The_Model_Never_Made_A_Token_Is_Reported()
    {
        var issues = await Issues<Parcel>(null);

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains("Parcel.Version carries [VersionStamp]") && i.Message.Contains("[ConcurrencyCheck]")));
    }

    // ── a token that is not declared a stamp ───────────────────────────────────

    [Test]
    public async Task A_Data_Column_The_Dtos_Lack_Is_Not_Reported()
    {
        // the runtime never compares a data column with the client's value, so the DTOs need not carry it
        var mapped = await Issues<Shipment>(new EntityMappingRegistration(typeof(Shipment), typeof(IdOnlyDto), typeof(IdOnlyDto)));
        var selfBound = await Issues<Shipment>(null);

        Assert.Multiple(() =>
        {
            Assert.That(mapped, Is.Empty);
            Assert.That(selfBound, Is.Empty);
        });
    }

    [Test]
    public async Task An_Undeclared_Token_Shaped_Like_A_Stamp_Is_Reported()
    {
        // compared as a data column, so a stale client is not refused; the DTOs need not carry it
        var issues = await Issues<Voucher>(new EntityMappingRegistration(typeof(Voucher), typeof(IdOnlyDto), typeof(IdOnlyDto)), minimum: LogLevel.Information);

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(issues[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(issues[0].Message, Does.Contain("Voucher.Version").And.Contain("[VersionStamp]").And.Contain("data column"));
        });
    }

    [Test]
    public async Task An_Undeclared_Timestamp_Token_Is_Reported()
    {
        var issues = await Issues<Ledger>(null);

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains("Ledger.LastModified") && i.Message.Contains("[VersionStamp]")));
    }

    [Test]
    public async Task An_Undeclared_Token_Of_A_Related_Child_Is_Reported()
    {
        // the child is written through its parent's sync, which compares the same way
        var issues = await Issues<Tour>(null, entity: e => e.Related(x => x.Stops));

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains("Stop.Version") && i.Message.Contains("[VersionStamp]")));
    }

    // ── false-positive guards ──────────────────────────────────────────────────

    [Test]
    public async Task A_Token_Both_Dtos_Carry_Is_Not_Reported()
    {
        var issues = await Issues<Order>(new EntityMappingRegistration(typeof(Order), typeof(OrderDto), typeof(OrderInputDto)));

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task A_Marker_On_The_Default_Wiring_Is_Not_Reported()
    {
        var issues = await Issues<Basket>(null);

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task A_Subclass_Of_The_Primer_Counts_As_Minting()
    {
        var issues = await Issues<Basket>(null, o =>
        {
            o.WireDbContext(DbContextWiring.All);
            o.AddPrimer<RotatingTokenPrimer>();
        });

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task A_Factory_Registered_Primer_Counts_As_Minting()
    {
        // a factory registration has no implementation type to inspect — only resolving it, as the interceptor does, can tell
        var issues = await Issues<Basket>(null, o => o.WireDbContext(DbContextWiring.All),
            services => services.AddTransient<IEntityPrimer>(_ => new HasConcurrencyTokenDbPrimer()));

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task An_Unmapped_Entity_Is_Not_Reported()
    {
        var issues = await Issues<Order>(null);

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task An_Entity_Without_A_Token_Is_Not_Reported()
    {
        var issues = await Issues<Note>(new EntityMappingRegistration(typeof(Note), typeof(BareDto), typeof(BareDto)));

        Assert.That(issues, Is.Empty);
    }
}
