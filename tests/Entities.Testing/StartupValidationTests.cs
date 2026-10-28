using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validators;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;

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

    [Test]
    public async Task Validation_Is_Skipped_Without_Environment_Or_OptIn()
    {
        // primers without interceptor would be warned about — but validation is off outside Development
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>()
            .For<Product>(e => e.Prime(_ => { }));

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.That(capture.Warnings, Is.Empty);
    }

    [Test]
    public async Task Missing_Primer_Interceptor_Warns_Without_Failing_Startup()
    {
        // An overridden SaveChanges applying primers manually is undetectable, so a missing interceptor
        // warns loudly instead of gating startup on a configuration the validator cannot see.
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Prime(_ => { }));

        using var sp = services.BuildServiceProvider();
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));

        Assert.That(capture.Warnings, Has.Some.Contains(nameof(ProductContext)).And.Some.Contains("WireDbContext"));
    }

    [Test]
    public async Task Wired_Primer_Interceptor_Passes_Validation()
    {
        // the interceptor check only warns, so the log is what shows it passed
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.WireDbContext(DbContextWiring.PrimerInterceptors);
            })
            .For<Product>(e => e.Prime(_ => { }));

        using var serviceProvider = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(serviceProvider);

        Assert.That(capture.Entries.Select(e => e.Message), Has.None.Contains("primer interceptor"));
    }

    [Test]
    public async Task Missing_Interceptor_Warning_Is_Logged_With_The_Fix_Snippet()
    {
        var capture = new StartupValidationHarness();
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
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains("WireDbContext"));
    }

    // Abstract-base registrations (UseEntities<Base>() + AddDbContext<Derived>()) must be validated
    // against the concrete context EF actually builds — not warn "could not inspect" on the base.
    [Test]
    public async Task Abstract_Base_Registration_Validates_The_Concrete_Context()
    {
        var capture = new StartupValidationHarness();
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
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));

        Assert.That(capture.Warnings, Has.None.Contains("Could not inspect"));
        Assert.That(capture.Warnings, Has.None.Contains("no primer interceptor"));
    }

    [Test]
    public async Task QSearch_Warning_For_Entities_Without_NormalizedContent()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        // Product implements IHasNormalizedTitle but not IHasNormalizedContent → ?q= is silently ignored
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.Some.Contains("?q=").And.Some.Contains(nameof(Product)));
    }

    // A custom filter makes ?q= work only when it reads Q; one that never does returns every row whatever q holds.
    // Shared by the positive and negative cases so a reworded warning fails the positive one instead of passing the others.
    private const string QIgnoredByFilterWarning = "?q= text search is ignored by the custom filters of";

    private async Task<StartupValidationHarness> ValidateProductFilter(Func<IQueryable<Product>, SearchObject<int>?, IQueryable<Product>> filter)
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Filter(filter));

        await using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);
        return capture;
    }

    [Test]
    public async Task QSearch_Warning_For_A_Custom_Filter_That_Never_Reads_Q()
    {
        var capture = await ValidateProductFilter((query, so) => so?.MinCreated == null ? query : query.Where(x => x.Created >= so.MinCreated));

        Assert.That(capture.Warnings, Has.Some.Contains(QIgnoredByFilterWarning).And.Some.Contains(nameof(Product)));
    }

    [Test]
    public async Task A_Custom_Filter_That_Reads_Q_Is_Not_Flagged()
    {
        var capture = await ValidateProductFilter((query, so) => string.IsNullOrWhiteSpace(so?.Q) ? query : query.Where(x => x.Title!.Contains(so.Q)));

        Assert.That(capture.Warnings, Has.None.Contains("?q="));
    }

    [Test]
    public async Task A_Custom_Filter_That_Reads_Q_Inside_The_Query_Is_Not_Flagged()
    {
        // no branch on Q: the query is the same shape either way, and only the captured search object holds the difference
        var capture = await ValidateProductFilter((query, so) => query.Where(x => so == null || so.Q == null || x.Title!.Contains(so.Q)));

        Assert.That(capture.Warnings, Has.None.Contains("?q="));
    }

    // a search by id reads Q only when it parses as a number
    [Test]
    public async Task A_Custom_Filter_That_Reads_Q_Only_As_A_Number_Is_Not_Flagged()
    {
        var capture = await ValidateProductFilter((query, so) => int.TryParse(so?.Q, out var id) ? query.Where(x => x.Id == id) : query);

        Assert.That(capture.Warnings, Has.None.Contains("?q="));
    }

    [Test]
    public async Task A_Custom_Filter_That_Captures_A_Collection_Taken_From_Q_Is_Not_Flagged()
    {
        var capture = await ValidateProductFilter((query, so) =>
        {
            var words = so?.Q?.Split(' ') ?? [];
            return query.Where(x => words.All(w => x.Title!.Contains(w)));
        });

        Assert.That(capture.Warnings, Has.None.Contains("?q="));
    }

    [Test]
    public async Task Regiras_Own_Attachment_Filters_Are_Not_Flagged()
    {
        // the attachment and link filters search by file name and leave Q alone by design; the app cannot change them
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ArchivedQueryFilterValidatorTests.AttachmentContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ArchivedQueryFilterValidatorTests.AttachmentContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = Path.GetTempPath() }))
            .For<ArchivedQueryFilterValidatorTests.Folder>(e => e.HasAttachments(x => x.Attachments));

        await using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains(QIgnoredByFilterWarning));
    }

    [Test]
    public async Task A_Clean_Validation_Says_So_Once()
    {
        var clean = await ValidateProductFilter((query, so) => string.IsNullOrWhiteSpace(so?.Q) ? query : query.Where(x => x.Title!.Contains(so.Q)));
        var flagged = await ValidateProductFilter((query, _) => query);

        Assert.Multiple(() =>
        {
            Assert.That(clean.Warnings.Concat(clean.Errors), Is.Empty);
            Assert.That(clean.Infos.Count(i => i.Contains("found no issues")), Is.EqualTo(1));
            Assert.That(flagged.Infos, Has.None.Contains("found no issues"));
        });
    }

    // a check that could not complete proves nothing: its skip warning stands alone, without the clean-run line
    [Test]
    public async Task A_Skipped_Check_Is_No_Clean_Run()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddSingleton<Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator, ThrowingValidator>();
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Filter((query, so) => string.IsNullOrWhiteSpace(so?.Q) ? query : query.Where(x => x.Title!.Contains(so.Q))));

        await using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Warnings, Has.Some.Contains("ThrowingValidator could not complete and was skipped"));
            Assert.That(capture.Infos, Has.None.Contains("found no issues"));
        });
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
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Related<ProductTag>(x => x.Tags))
            .For<ProductTag>();

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.Some.Contains(TwoWritePathsWarning).And.Some.Contains(nameof(ProductTag)));
    }

    [Test]
    public async Task Related_Child_Without_Its_Own_Registration_Is_Not_Flagged()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Related<ProductTag>(x => x.Tags));

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains(TwoWritePathsWarning));
        Assert.That(capture.Warnings, Has.None.Contains(nameof(ProductTag)));
    }

    public class Note : Regira.Entities.Models.Abstractions.IEntity<int> { public int Id { get; set; } }
    public class NoteContext(DbContextOptions<NoteContext> options) : DbContext(options)
    {
        public DbSet<Note> Notes => Set<Note>();
    }

    [Test]
    public async Task Second_Context_Without_PrimeAble_Entities_Does_Not_Fail_Startup()
    {
        // Primers registered for ProductContext's Product must not ask for a primer interceptor on an unrelated
        // NoteContext whose entity has no primers. The check only warns, so the log is what shows it.
        var noteConnection = new SqliteConnection("Filename=:memory:");
        noteConnection.Open();
        try
        {
            var capture = new StartupValidationHarness();
            var services = new ServiceCollection();
            services.AddLogging(b => b.AddProvider(capture));
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
            await StartupValidationHarness.RunHostedServices(sp);

            Assert.That(capture.Entries.Select(e => e.Message), Has.None.Contains(nameof(NoteContext)));
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
    public async Task Validation_Message_With_Braces_Does_Not_Crash_Logging()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddSingleton<Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator, BracedMessageValidator>();
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true)).For<Product>();

        using var sp = services.BuildServiceProvider();
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains("{braces}"));
    }

    [Test]
    public async Task Typed_Only_Primer_Without_Interceptor_Is_Flagged()
    {
        // A primer registered only under IEntityPrimer<Product> is not returned by GetServices<IEntityPrimer>();
        // the validator must still see it (via the service type) and warn about the missing interceptor.
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection)); // no interceptor
        services.AddTransient<IEntityPrimer<Product>, ProductMarkerPrimer>();
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true)).For<Product>();

        using var sp = services.BuildServiceProvider();
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains("WireDbContext"));
    }

    [Test]
    public async Task Primer_Resolution_Failure_Warns_Instead_Of_Crashing_Startup()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddTransient<IEntityPrimer>(_ => throw new InvalidOperationException("boom")); // misconfigured factory
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true)).For<Product>();

        using var sp = services.BuildServiceProvider();
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp)); // a diagnostic must not crash the host
        Assert.That(capture.Warnings, Has.Some.Contains("InterceptorWiringValidator could not complete and was skipped"));
    }

    private sealed class ThrowingValidator : Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator
    {
        public IEnumerable<Regira.Entities.DependencyInjection.Validation.EntityValidationIssue> Validate(Regira.Entities.DependencyInjection.Validation.EntityValidationContext context)
            => throw new InvalidOperationException("boom");
    }

    private sealed class ReportThenThrowValidator : Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator
    {
        public IEnumerable<Regira.Entities.DependencyInjection.Validation.EntityValidationIssue> Validate(Regira.Entities.DependencyInjection.Validation.EntityValidationContext context)
        {
            yield return new Regira.Entities.DependencyInjection.Validation.EntityValidationIssue(
                Regira.Entities.DependencyInjection.Validation.EntityValidationSeverity.Error, "found before the throw");
            throw new InvalidOperationException("boom");
        }
    }

    [Test]
    public async Task A_Check_That_Throws_Keeps_What_It_Reported_Before()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddSingleton<Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator, ReportThenThrowValidator>();
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true)).For<Product>();

        using var sp = services.BuildServiceProvider();
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => StartupValidationHarness.RunHostedServices(sp));

        Assert.Multiple(() =>
        {
            Assert.That(ex!.Message, Does.Contain("found before the throw"), "the Error still stops the host");
            Assert.That(capture.Warnings, Has.Some.Contains("ReportThenThrowValidator could not complete and was skipped"));
        });
    }

    [Test]
    public async Task A_Throwing_Validator_Neither_Stops_The_Host_Nor_Silences_The_Others()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.AddSingleton<Regira.Entities.DependencyInjection.Validation.IEntityRegistrationValidator, ThrowingValidator>();
        services.UseEntities<ProductContext>(o =>
            {
                o.ConfigureValidation(v => v.Enabled = true);
                o.AddGlobalFilterQueryBuilder<OutOfScopeFilter>();
            })
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.Multiple(() =>
        {
            Assert.That(capture.Warnings, Has.Some.Contains("ThrowingValidator could not complete and was skipped"));
            Assert.That(capture.Warnings, Has.Some.Contains(nameof(OutOfScopeFilter)), "the other checks still report");
        });
    }

    [Test]
    public async Task A_Context_Without_A_Provider_Does_Not_Hide_The_Errors_Of_Another()
    {
        // the context resolves, but reading its model throws: the checks skip it and still judge the other context
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>();
        services.UseEntities<ProductContext>().For<Product>();
        services.AddDbContext<ConcurrencyTokenValidatorTests.ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ConcurrencyTokenValidatorTests.ShopContext>(o =>
            {
                o.UseDefaults();
                o.WireDbContext(DbContextWiring.All & ~DbContextWiring.ConcurrencyTokens);
                o.ConfigureValidation(v =>
                {
                    v.Enabled = true;
                    v.ThrowOnError = false;
                });
            })
            .For<ConcurrencyTokenValidatorTests.Basket>();

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Errors, Has.Some.Contains(nameof(ConcurrencyTokenValidatorTests.Basket)).And.Some.Contains("DbContextWiring.ConcurrencyTokens"));
            Assert.That(capture.Warnings.Where(w => w.Contains($"Could not resolve {nameof(ProductContext)}")).ToArray(), Has.Length.EqualTo(1));
        });
    }

    [Test]
    public async Task An_Unresolvable_Context_Is_Reported_Once()
    {
        // every check that reads the model skips it, and the run says so once rather than once per check
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.UseEntities<ProductContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Product>();
        services.AddScoped<ProductContext>(_ => throw new InvalidOperationException("no database provider"));

        using var sp = services.BuildServiceProvider();
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.That(capture.Warnings.Where(w => w.Contains($"Could not resolve {nameof(ProductContext)}")).ToArray(),
            Has.Exactly(1).Contains("no database provider"));
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
    public async Task Global_Filter_Depending_On_A_Scoped_Service_Is_Still_Checked()
    {
        // Resolving the filters from the root provider threw on the scoped dependency and downgraded the whole
        // check to a single "could not resolve" line — disabling it for exactly the filters it exists to guard.
        var capture = new StartupValidationHarness();
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
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.That(capture.Infos, Has.None.Contains("Could not resolve global filters"));
        Assert.That(capture.Warnings, Has.Some.Contains(nameof(CallerScopedFilter)));
    }

    [Test]
    public async Task Global_Filter_Matching_No_Registered_Entity_Warns()
    {
        var capture = new StartupValidationHarness();
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
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.Some.Contains(nameof(OutOfScopeFilter)));
    }

    [Test]
    public async Task Global_Filter_Scoped_To_The_Concrete_Entity_Does_Not_Warn()
    {
        // Scoping the concrete entity type is valid and must not be reported as inert.
        var capture = new StartupValidationHarness();
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
        await Assert.DoesNotThrowAsync(() => StartupValidationHarness.RunHostedServices(sp));
        Assert.That(capture.Warnings, Has.None.Contains(nameof(ProductScopedFilter)));
    }

    [Test]
    public async Task Regiras_Own_Global_Filters_Are_Never_Reported()
    {
        // UseDefaults() registers the whole built-in set regardless of the app's entities: Product is not IArchivable,
        // so the archive filter can never run, whether the defaults or the app registered it
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o =>
            {
                o.UseDefaults();
                o.AddGlobalFilterQueryBuilder<FilterArchivablesQueryBuilder<Guid>>();
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Product>();

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.Multiple(() =>
        {
            Assert.That(capture.Warnings, Has.None.Contains("never runs"));
            Assert.That(capture.Infos, Has.None.Contains("never run"));
        });
    }

    // A flag-gated Includes() on a simple registration is Details-only — a real trap, but one that lives in
    // the callback body, which no startup check can see. It is documented (entities.instructions → Step 4)
    // rather than validated: warning on every simple registration that calls Includes() would fire on the
    // correct flag-independent shape too, including the ones the attachment services register internally.
    [Test]
    public async Task Includes_On_A_Simple_Registration_Is_Not_Reported()
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ProductContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Product>(e => e.Includes((query, _) => query));

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

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
    public async Task ServerOwned_On_The_Archived_Flag_And_On_A_Navigation_Fail_Startup()
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
        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => StartupValidationHarness.RunHostedServices(sp));

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
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<OrderContext>(db => db.UseSqlite(_connection));
        services.UseEntities<OrderContext>(o => o.ConfigureValidation(v => v.Enabled = true))
            .For<Note>(e => e.Related<InvoiceLine>(_ => null));

        using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.None.Contains("AutoServerOwnedPrepper"));
    }

    [Test]
    public async Task ServerOwned_Without_Enforcement_Warns()
    {
        var capture = new StartupValidationHarness();
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
        await StartupValidationHarness.RunHostedServices(sp);

        Assert.That(capture.Warnings, Has.Some.Contains("no AutoServerOwnedPrepper is registered"));
    }

    // ── entity validators ───────────────────────────────────────────────────────

    /// <summary>A write service that still calls the constructor without validators.</summary>
    public class LegacyProductWriteService(ProductContext dbContext, IEntityReadService<Product, int> readService,
        IEnumerable<IEntityPrepper> preppers, ILoggerFactory? loggerFactory = null)
        : EntityWriteService<ProductContext, Product>(dbContext, readService, preppers, loggerFactory);

    /// <summary>A wrapping service: the check follows it to the repository and write service behind it.</summary>
    public class AuditedProductService(IEntityRepository<Product, int, Regira.Entities.Models.SearchObject<int>> repository) : EntityWrappingServiceBase<Product>(repository);

    private async Task<StartupValidationHarness> StartWithValidation(Action<Regira.Entities.DependencyInjection.ServiceCollections.EntityServiceCollection<ProductContext>> configure,
        Action<EntityServiceCollectionOptions>? options = null)
    {
        var capture = new StartupValidationHarness();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<ProductContext>(db => db.UseSqlite(_connection));
        configure(services.UseEntities<ProductContext>(o =>
        {
            o.ConfigureValidation(v => v.Enabled = true);
            options?.Invoke(o);
        }));

        await using var sp = services.BuildServiceProvider();
        await StartupValidationHarness.RunHostedServices(sp);
        return capture;
    }

    [Test]
    public async Task An_Entity_Validator_No_Registered_Entity_Is_In_Scope_Of_Warns()
    {
        // Category is written only through the Product it belongs to here — it has no For<>() — so a validator scoped
        // to it never runs
        var capture = await StartWithValidation(
            s => s.For<Product>(e => e.Validate(_ => { })),
            o => o.AddValidator<Category>(_ => { }));

        Assert.Multiple(() =>
        {
            Assert.That(capture.Warnings, Has.Some.Contains("EntityValidator<Category>").And.Some.Contains("never runs"));
            Assert.That(capture.Warnings, Has.None.Contains("EntityValidator<Product>"));
        });
    }

    [Test]
    public async Task A_Write_Service_On_The_Constructor_Without_Validators_Warns()
    {
        var capture = await StartWithValidation(s => s.For<Product>(e => e
            .UseEntityService<AuditedProductService>()
            .UseWriteService<LegacyProductWriteService>()
            .Validate(_ => { })));

        Assert.That(capture.Warnings, Has.Some.Contains("'LegacyProductWriteService'").And.Some.Contains("IEnumerable<IEntityValidator>"));
    }

    [Test]
    public async Task A_Write_Path_That_Reaches_The_Validators_Is_Silent()
    {
        var capture = await StartWithValidation(s => s.For<Product>(e => e
            .UseEntityService<AuditedProductService>()
            .Validate(_ => { })));

        Assert.That(capture.Warnings, Has.None.Contains("IEntityValidator"));
    }

    [Test]
    public async Task A_Write_Service_Without_Validators_In_Scope_Is_Not_Reported()
    {
        var capture = await StartWithValidation(s => s.For<Product>(e => e.UseWriteService<LegacyProductWriteService>()));

        Assert.That(capture.Warnings, Has.None.Contains("IEntityValidator"));
    }
}
