using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;

namespace Entities.Testing;

[TestFixture]
public class StartupValidationTests
{
    private SqliteConnection _connection = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }
    [TearDown]
    public void TearDown() => _connection.Close();

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Warnings { get; } = [];
        public List<string> Errors { get; } = [];
        public List<string> Infos { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }

        private sealed class CaptureLogger(CaptureLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning) provider.Warnings.Add(formatter(state, exception));
                if (logLevel == LogLevel.Error) provider.Errors.Add(formatter(state, exception));
                if (logLevel == LogLevel.Information) provider.Infos.Add(formatter(state, exception));
            }
        }
    }

    private static async Task RunHostedServices(ServiceProvider serviceProvider)
    {
        foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    [Test]
    public void Validation_Is_Skipped_Without_Environment_Or_OptIn()
    {
        // primers without interceptor would be an error — but validation is off outside Development
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>()
            .For<Product>(e => e.Prime(_ => { }));

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
    }

    [Test]
    public void Missing_Primer_Interceptor_Warns_Without_Failing_Startup()
    {
        // An overridden SaveChanges applying primers manually is undetectable, so a missing interceptor
        // warns loudly instead of gating startup on a configuration the validator cannot see.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Prime(_ => { }));

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));

        Assert.That(capture.Warnings, Has.Some.Contains(nameof(ProductContext)).And.Some.Contains("WireDbContext"));
    }

    [Test]
    public void Wired_Primer_Interceptor_Passes_Validation()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.WireDbContext(DbContextWiring.PrimerInterceptors);
            })
            .For<Product>(e => e.Prime(_ => { }));

        using var serviceProvider = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(serviceProvider));
    }

    [Test]
    public void Missing_Interceptor_Warning_Is_Logged_With_The_Fix_Snippet()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v =>
            {
                v.Enabled = true;
                v.ThrowOnError = false;
            }))
            .For<Product>(e => e.Prime(_ => { }));

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains("WireDbContext"));
    }

    // Abstract-base registrations (UseEntities<Base>() + AddDbContext<Derived>()) must be validated
    // against the concrete context EF actually builds — not warn "could not inspect" on the base.
    [Test]
    public void Abstract_Base_Registration_Validates_The_Concrete_Context()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<UseDefaultsAutoWiringTests.DerivedContext>(db => db.UseSqlite(_connection));
        services.UseEntities<UseDefaultsAutoWiringTests.BaseContext>(o =>
            {
                o.UseDefaults(); // wires the concrete context via the assignability match
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Product>(e => e.Prime(_ => { }));

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));

        Assert.That(capture.Warnings, Has.None.Contains("Could not inspect"));
        Assert.That(capture.Warnings, Has.None.Contains("no primer interceptor"));
    }

    [Test]
    public async Task QSearch_Warning_For_Entities_Without_NormalizedContent()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        // Product implements IHasNormalizedTitle but not IHasNormalizedContent → ?q= is silently ignored
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.Some.Contains("?q=").And.Some.Contains(nameof(Product)));
    }

    // Shared by the positive and negative cases below so a reworded warning fails the positive test loudly
    // instead of silently turning the negative one vacuous (it previously drifted to a stale literal).
    private const string TwoWritePathsWarning = "Two write paths";

    [Test]
    public async Task Related_Child_With_Its_Own_Registration_Warns_About_Two_Write_Paths()
    {
        // The pairing is supported, but only while the parent's input DTO leaves Tags null (the sync then
        // short-circuits). If Product ever saves with Tags populated, the re-diff silently overwrites rows
        // written through the standalone ProductTag service. Registrations are all the validator can see —
        // it can't inspect DTO shapes — so it flags the pairing and leaves the judgement to the developer.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Related<ProductTag>(x => x.Tags))
            .For<ProductTag>();

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.Some.Contains(TwoWritePathsWarning).And.Some.Contains(nameof(ProductTag)));
    }

    [Test]
    public async Task Related_Child_Without_Its_Own_Registration_Is_Not_Flagged()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Related<ProductTag>(x => x.Tags));

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains(TwoWritePathsWarning));
        Assert.That(capture.Warnings, Has.None.Contains(nameof(ProductTag)));
    }

    public class Note : Regira.Entities.Models.Abstractions.IEntity<int> { public int Id { get; set; } }
    public class NoteContext(DbContextOptions<NoteContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }

    [Test]
    public void Second_Context_Without_PrimeAble_Entities_Does_Not_Fail_Startup()
    {
        // Regression: primers registered for ProductContext's Product must not force a primer interceptor
        // on an unrelated NoteContext whose entity has no primers.
        var noteConnection = new SqliteConnection("Filename=:memory:");
        noteConnection.Open();
        try
        {
            var services = new ServiceCollection();
            services.AddLogging();
            services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
            services.AddDbContext<NoteContext>(db => db.UseSqlite(noteConnection)); // deliberately no interceptor
            services.UseEntities<ProductContext>(o =>
                {
                    o.ConfigureValidation(v => v.Enabled = true);
                    o.WireDbContext(DbContextWiring.PrimerInterceptors);
                })
                .For<Product>(e => e.Prime(_ => { }));
            services.UseEntities<NoteContext>()
                .For<Note>();

            using var sp = services.BuildServiceProvider();
            Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
        }
        finally
        {
            noteConnection.Close();
        }
    }

    // ── Lower-confidence panel items (review 2026-07-12) ────────────────────────────────────────────

    private sealed class BracedMessageValidator : Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator
    {
        public IEnumerable<Regira.Entities.DependencyInjection.Validation.EntityValidationIssue> Validate(
            Regira.Entities.DependencyInjection.Validation.EntityValidationContext context)
        {
            // a message containing '{'/'}' (a code snippet) must not be treated as a log template
            yield return new Regira.Entities.DependencyInjection.Validation.EntityValidationIssue(
                Regira.Entities.DependencyInjection.Validation.EntityValidationSeverity.Warning,
                "Config issue near services.AddDbContext<T>((sp, o) => { o.UseSqlite(); }) — check {braces}");
        }
    }

    [Test]
    public void Validation_Message_With_Braces_Does_Not_Crash_Logging()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddSingleton<Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator, BracedMessageValidator>();
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true)).For<Product>();

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains("{braces}"));
    }

    [Test]
    public void Typed_Only_Primer_Without_Interceptor_Is_Flagged()
    {
        // A primer registered only under IEntityPrimer<Product> is not returned by GetServices<IEntityPrimer>();
        // the validator must still see it (via the service type) and warn about the missing interceptor.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection)); // no interceptor
        services.AddTransient<IEntityPrimer<Product>, ProductMarkerPrimer>();
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true)).For<Product>();

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains("WireDbContext"));
    }

    [Test]
    public void Primer_Resolution_Failure_Warns_Instead_Of_Crashing_Startup()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddTransient<IEntityPrimer>(_ => throw new InvalidOperationException("boom")); // misconfigured factory
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true)).For<Product>();

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp)); // a diagnostic must not crash the host
        Assert.That(capture.Warnings, Has.Some.Contains("Could not resolve primers"));
    }

    private sealed class ProductMarkerPrimer : Regira.Entities.EFcore.Primers.Abstractions.EntityPrimerBase<Product>
    {
        public override Task PrepareAsync(Product entity, Microsoft.EntityFrameworkCore.ChangeTracking.EntityEntry entry, CancellationToken token = default)
            => Task.CompletedTask;
    }

    // A global filter scoped to a type no registered entity satisfies never runs. Silent, and
    // security-relevant whenever the filter is the one restricting row access.
    private interface IUnusedScope;
    private sealed class OutOfScopeFilter : GlobalFilteredQueryBuilderBase<IUnusedScope>
    {
        public override IQueryable<IUnusedScope> Build(IQueryable<IUnusedScope> query, ISearchObject<int>? so)
            => query;
    }
    private sealed class ProductScopedFilter : GlobalFilteredQueryBuilderBase<Product>
    {
        public override IQueryable<Product> Build(IQueryable<Product> query, ISearchObject<int>? so)
            => query;
    }

    // The shape of every row-security filter: it needs the caller, so it depends on a request-scoped service.
    private interface ICaller { bool IsAdministrator { get; } }
    private sealed class Caller : ICaller { public bool IsAdministrator => false; }
    private sealed class CallerScopedFilter(ICaller caller) : GlobalFilteredQueryBuilderBase<IUnusedScope>
    {
        public override IQueryable<IUnusedScope> Build(IQueryable<IUnusedScope> query, ISearchObject<int>? so)
            => caller.IsAdministrator ? query : query.Take(0);
    }

    [Test]
    public void Global_Filter_Depending_On_A_Scoped_Service_Is_Still_Checked()
    {
        // Resolving the filters from the root provider threw on the scoped dependency and downgraded the whole
        // check to a single "could not resolve" line — disabling it for exactly the filters it exists to guard.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddScoped<ICaller, Caller>();
        services.UseEntities<ProductContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.AddGlobalFilterQueryBuilder<CallerScopedFilter>();
            })
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
        Assert.That(capture.Infos, Has.None.Contains("Could not resolve global filters"));
        Assert.That(capture.Warnings, Has.Some.Contains(nameof(CallerScopedFilter)));
    }

    [Test]
    public void Global_Filter_Matching_No_Registered_Entity_Warns()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.AddGlobalFilterQueryBuilder<OutOfScopeFilter>();
            })
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains(nameof(OutOfScopeFilter)));
    }

    [Test]
    public void Global_Filter_Scoped_To_The_Concrete_Entity_Does_Not_Warn()
    {
        // Scoping the concrete entity type is valid and must not be reported as inert.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.AddGlobalFilterQueryBuilder<ProductScopedFilter>();
            })
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        Assert.DoesNotThrowAsync(() => RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.None.Contains(nameof(ProductScopedFilter)));
    }

    [Test]
    public async Task Inert_Builtin_Global_Filters_Are_Informational_Not_Warnings()
    {
        // UseDefaults() registers the whole built-in set regardless of the app's entities. Product is not
        // IArchivable and has no timestamps, so several of them can never run — expected, and reporting each
        // with the security wording would train readers to skim past the one that matters.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains("FilterArchivables"), "built-in inert filter warned");
        Assert.That(capture.Warnings, Has.None.Contains("never runs"));
        Assert.That(capture.Infos, Has.Some.Contains("Built-in global filters that never run"));
    }

    [Test]
    public async Task Deliberately_Registered_Builtin_That_Never_Runs_Still_Warns()
    {
        // Registering FilterArchivablesQueryBuilder by hand states an intent: these rows should be
        // soft-deleted. If no entity implements IArchivable that intent is silently unmet, which is the
        // security case this validator exists for — so it must not be absorbed into the defaults' Info line.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.AddGlobalFilterQueryBuilder<FilterArchivablesQueryBuilder>();
            })
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.Some.Contains("FilterArchivablesQueryBuilder").And.Some.Contains("never runs"));
    }

    public class GuidNote : Regira.Entities.Models.Abstractions.IEntity<Guid> { public Guid Id { get; set; } }
    public class GuidNoteContext(DbContextOptions<GuidNoteContext> options) : DbContext(options)
    {
        public DbSet<GuidNote> Notes => Set<GuidNote>();
    }

    [Test]
    public async Task Inert_Keyed_Variants_Of_The_Builtins_Are_Informational_Too()
    {
        // For<TEntity, TKey>() back-fills the TKey twins of the int-keyed defaults, and only because the
        // defaults registered the int ones — so they are defaults-contributed just as much. A plain
        // Guid-keyed entity implements none of the capabilities they scope, which makes every twin inert:
        // reporting those as hand-registered filters puts four security warnings in front of an app that
        // asked for none of them.
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<GuidNoteContext>(db => db.UseSqlite(_connection));
        services.UseEntities<GuidNoteContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<GuidNote, Guid>();

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains("never runs"), "a keyed built-in twin warned");
        Assert.That(capture.Infos, Has.Some.Contains("Built-in global filters that never run"));
    }

    // A flag-gated Includes() on a simple registration is Details-only — a real trap, but one that lives in
    // the callback body, which no startup check can see. It is documented (entities.instructions → Step 4)
    // rather than validated: warning on every simple registration that calls Includes() would fire on the
    // correct flag-independent shape too, including the ones the attachment services register internally.
    [Test]
    public async Task Includes_On_A_Simple_Registration_Is_Not_Reported()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Includes((query, _) => query));

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains("Includes()"));
    }

    public class Invoice : Regira.Entities.Models.Abstractions.IEntity<int>, Regira.Entities.Models.Abstractions.IArchivable
    {
        public int Id { get; set; }
        [Regira.Entities.Attributes.ServerOwned] public string? Code { get; set; }
        [Regira.Entities.Attributes.ServerOwned] public bool IsArchived { get; set; }
        [Regira.Entities.Attributes.ServerOwned] public ICollection<Note>? Notes { get; set; }
    }
    public class InvoiceContext(DbContextOptions<InvoiceContext> options) : DbContext(options)
    {
        public DbSet<Invoice> Invoices => Set<Invoice>();
        public DbSet<Note> Notes => Set<Note>();
    }

    [Test]
    public void ServerOwned_On_The_Archived_Flag_And_On_A_Navigation_Fail_Startup()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<InvoiceContext>(db => db.UseSqlite(_connection));
        services.UseEntities<InvoiceContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Invoice>();

        using var sp = services.BuildServiceProvider();
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => RunHostedServices(sp));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("Invoice.IsArchived"));
            Assert.That(ex.Message, Does.Contain("Invoice.Notes"));
            Assert.That(ex.Message, Does.Not.Contain("Invoice.Code"));
        });
    }

    public class InvoiceLine : Regira.Entities.Models.Abstractions.IEntity<int>
    {
        public int Id { get; set; }
        public int InvoiceId { get; set; }
        [Regira.Entities.Attributes.ServerOwned] public decimal UnitPrice { get; set; }
    }
    public class OrderContext(DbContextOptions<OrderContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
        public DbSet<InvoiceLine> InvoiceLines => Set<InvoiceLine>();
    }

    /// <summary>
    /// A Related() child is prepped through its parent's chain, which carries the [ServerOwned] restore
    /// whether or not UseDefaults() ran — so reporting it as unenforced would name an enforced field.
    /// </summary>
    [Test]
    public async Task ServerOwned_On_A_Related_Child_Alone_Does_Not_Warn()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<OrderContext>(db => db.UseSqlite(_connection));
        services.UseEntities<OrderContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Note>(e => e.Related<InvoiceLine>(_ => null));

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains("AutoServerOwnedPrepper"));
    }

    [Test]
    public async Task ServerOwned_Without_Enforcement_Warns()
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<InvoiceContext>(db => db.UseSqlite(_connection));
        services.UseEntities<InvoiceContext>(o => o.ConfigureValidation(v =>
            {
                v.Enabled = true;
                v.ThrowOnError = false;
            }))
            .For<Invoice>();

        using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.Some.Contains("no AutoServerOwnedPrepper is registered"));
    }
}
