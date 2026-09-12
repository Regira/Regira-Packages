using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Entities.Testing;

/// <summary>
/// The startup check for a concurrency token that is never checked, never moves, or cannot make the round trip to
/// the client. The write path compares a token with the value the client sent, so a DTO without the property
/// silently turns every write into last-write-wins — and an initializer the mapper leaves in place turns every write
/// into a 409.
/// </summary>
[TestFixture]
public class ConcurrencyTokenValidatorTests
{
    private const string Hazard = "concurrency token";

    public class Order : IEntity<int>
    {
        public int Id { get; set; }
        public string? Status { get; set; }
        [ConcurrencyCheck] public Guid Version { get; set; }
    }

    /// The trap: a token initializer, which a fresh mapped entity keeps when no DTO property overwrites it.
    public class Invoice : IEntity<int>
    {
        public int Id { get; set; }
        [ConcurrencyCheck] public Guid Version { get; set; } = Guid.NewGuid();
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

    public record InitializingInputDto
    {
        public int Id { get; set; }
        public Guid Version { get; set; } = Guid.NewGuid();
    }

    /// A consumer's own variant of the built-in primer — it mints, so it must count.
    public class RotatingTokenPrimer : HasConcurrencyTokenDbPrimer;

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Order> Orders => Set<Order>();
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Basket> Baskets => Set<Basket>();
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<(LogLevel Level, string Message)> Entries { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }

        private sealed class CaptureLogger(CaptureLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel >= LogLevel.Warning) provider.Entries.Add((logLevel, formatter(state, exception)));
            }
        }
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
    /// The startup issues about concurrency tokens, for one entity registered with the given mapping. The defaults are
    /// <c>UseDefaults()</c>; <paramref name="configure"/> replaces them, and <paramref name="register"/> adds services
    /// of its own.
    /// </summary>
    private async Task<List<(LogLevel Level, string Message)>> Issues<TEntity>(EntityMappingRegistration? mapping,
        Action<EntityServiceCollectionOptions>? configure = null, Action<IServiceCollection>? register = null)
        where TEntity : class, IEntity<int>
    {
        var capture = new CaptureLoggerProvider();
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
            .For<TEntity>();
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
        return capture.Entries.Where(e => e.Message.Contains(Hazard)).ToList();
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

    [Test]
    public async Task A_Token_The_Read_Dto_Does_Not_Return_Is_Reported()
    {
        // the client can send a token back only if it received one
        var issues = await Issues<Order>(new EntityMappingRegistration(typeof(Order), typeof(BareDto), typeof(OrderInputDto)));

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains(nameof(BareDto))));
    }

    [Test]
    public async Task An_Initialized_Token_The_Input_Dto_Cannot_Overwrite_Is_An_Error()
    {
        var issues = await Issues<Invoice>(new EntityMappingRegistration(typeof(Invoice), typeof(BareDto), typeof(BareDto)));

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(issues[0].Level, Is.EqualTo(LogLevel.Error));
            Assert.That(issues[0].Message, Does.Contain("Invoice.Version").And.Contain("answers 409"));
        });
    }

    [Test]
    public async Task An_Input_Dto_That_Initializes_The_Token_Is_Reported()
    {
        var issues = await Issues<Order>(new EntityMappingRegistration(typeof(Order), typeof(OrderDto), typeof(InitializingInputDto)));

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains($"{nameof(InitializingInputDto)}.Version has an initializer")));
    }

    [Test]
    public async Task An_Unmapped_Entity_That_Initializes_Its_Token_Is_Reported()
    {
        // entity-as-DTO: the token travels with the entity, but a request that omits it carries the initializer's value
        var issues = await Issues<Invoice>(null);

        Assert.That(issues, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(issues[0].Level, Is.EqualTo(LogLevel.Warning));
            Assert.That(issues[0].Message, Does.Contain("Invoice.Version").And.Contain("is its own input DTO"));
        });
    }

    [Test]
    public async Task An_Entity_Mapped_Onto_Itself_Is_Judged_Like_An_Unmapped_One()
    {
        var issues = await Issues<Invoice>(new EntityMappingRegistration(typeof(Invoice), typeof(Invoice), typeof(Invoice)));

        Assert.That(issues, Has.Exactly(1).Matches<(LogLevel Level, string Message)>(i =>
            i.Level == LogLevel.Warning && i.Message.Contains("is its own input DTO")));
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

    // ── false-positive guards ──────────────────────────────────────────────────

    [Test]
    public async Task A_Token_Both_Dtos_Carry_Is_Not_Reported()
    {
        var issues = await Issues<Order>(new EntityMappingRegistration(typeof(Order), typeof(OrderDto), typeof(OrderInputDto)));

        Assert.That(issues, Is.Empty);
    }

    [Test]
    public async Task An_Initialized_Token_The_Input_Dto_Overwrites_Is_Not_An_Error()
    {
        // the DTO's own value replaces the initializer on every request, so the initializer never reaches the row
        var issues = await Issues<Invoice>(new EntityMappingRegistration(typeof(Invoice), typeof(OrderDto), typeof(OrderInputDto)));

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
    public async Task An_Unmapped_Entity_Without_An_Initializer_Is_Not_Reported()
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
