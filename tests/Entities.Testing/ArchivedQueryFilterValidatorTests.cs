using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// The startup gate for the one soft-delete misconfiguration nothing else catches: an
/// <see cref="IArchivable"/> entity whose model carries no archived query filter — neither from the options
/// wiring (<c>DbContextWiring.ArchivedQueryFilter</c>, on by default) nor from an explicit
/// <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/>. Everything compiles, DI is satisfied and the
/// app starts — while <c>DELETE</c> flags rows nobody hides.
/// <para>
/// Silent on <c>net8.0</c> by design: no archived query filter is installed there (EF Core 9 has no named
/// filters, and the untargeted opt-out would suspend the app's own row security), and the query builder
/// excludes archived rows at the root of the query instead — so there is nothing to be missing.
/// </para>
/// </summary>
[TestFixture]
public class ArchivedQueryFilterValidatorTests
{
    // The wording the validator leads with; shared so a rename fails the positive test loudly instead of
    // quietly making the negative ones vacuous.
    private const string MissingFilterError = "IArchivable without a soft-delete filter";

    public class Doc : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
    }

    /// <summary>Archivable, plus the entity types <c>HasAttachments()</c> brings into the model.</summary>
    public class Folder : IEntity<int>, IArchivable, IHasAttachments, IHasAttachments<FolderAttachment>
    {
        public int Id { get; set; }
        public bool IsArchived { get; set; }

        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<FolderAttachment>().ToArray();
        }
        public ICollection<FolderAttachment>? Attachments { get; set; }
        public bool? HasAttachment { get; set; }
    }

    public class FolderAttachment : EntityAttachment
    {
        public FolderAttachment() => ObjectType = nameof(Folder);
    }

    /// <summary>A TPH hierarchy: EF takes the filter on the root only, so the derived type must not be flagged.</summary>
    public class Record : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public bool IsArchived { get; set; }
    }
    public class SpecialRecord : Record;

    public class GuidDoc : IEntity<Guid>, IArchivable
    {
        public Guid Id { get; set; }
        public bool IsArchived { get; set; }
    }

    public class FilteredContext(DbContextOptions<FilteredContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    /// <summary>The defect: an IArchivable entity and no <c>SetArchivedQueryFilter()</c>.</summary>
    public class UnfilteredContext(DbContextOptions<UnfilteredContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();
    }

    public class AttachmentContext(DbContextOptions<AttachmentContext> options) : DbContext(options)
    {
        public DbSet<Folder> Folders => Set<Folder>();
        public DbSet<FolderAttachment> FolderAttachments => Set<FolderAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Folder>()
                .HasMany(x => x.Attachments)
                .WithOne()
                .HasForeignKey(x => x.ObjectId)
                .HasPrincipalKey(x => x.Id);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    public class HierarchyContext(DbContextOptions<HierarchyContext> options) : DbContext(options)
    {
        public DbSet<Record> Records => Set<Record>();
        public DbSet<SpecialRecord> SpecialRecords => Set<SpecialRecord>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    public class GuidContext(DbContextOptions<GuidContext> options) : DbContext(options)
    {
        public DbSet<GuidDoc> Docs => Set<GuidDoc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    /// <summary>No IArchivable entity at all — the validator has nothing to say.</summary>
    public class Plain : IEntity<int> { public int Id { get; set; } }
    public class PlainContext(DbContextOptions<PlainContext> options) : DbContext(options)
    {
        public DbSet<Plain> Items => Set<Plain>();
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Errors { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }

        private sealed class CaptureLogger(CaptureLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Error) provider.Errors.Add(formatter(state, exception));
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

    private static async Task RunHostedServices(ServiceProvider serviceProvider)
    {
        foreach (var hostedService in serviceProvider.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
    }

    /// <summary>Runs startup validation with errors logged instead of thrown, and returns the error lines.</summary>
    private async Task<List<string>> Errors(Action<IServiceCollection> configure)
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        configure(services);

        await using var sp = services.BuildServiceProvider();
        await RunHostedServices(sp);
        return capture.Errors;
    }

    private static Action<Regira.Entities.DependencyInjection.ServiceCollections.Models.EntityServiceCollectionOptions> Logging(bool throwOnError = false)
        => o => o.ConfigureValidation(v =>
        {
            v.Enabled = true;
            v.ThrowOnError = throwOnError;
        });

    /// <summary>
    /// The defect shape: an <see cref="IArchivable"/> entity, no <c>SetArchivedQueryFilter()</c> in the
    /// <c>DbContext</c>, <em>and</em> the options wiring that would otherwise install the filter turned off —
    /// which is what a non-generic <c>UseEntities()</c> or a hand-picked <c>WireDbContext(...)</c> leaves behind.
    /// </summary>
    private static void Unwired(Regira.Entities.DependencyInjection.ServiceCollections.Models.EntityServiceCollectionOptions o)
        => o.WireDbContext(Regira.Entities.DependencyInjection.ServiceCollections.Models.DbContextWiring.All
                           & ~Regira.Entities.DependencyInjection.ServiceCollections.Models.DbContextWiring.ArchivedQueryFilter);

    // ── the defect ─────────────────────────────────────────────────────────────

    [Test]
    public async Task A_Missing_Archived_Query_Filter_Is_Reported()
    {
        var errors = await Errors(services =>
        {
            services.AddDbContext<UnfilteredContext>(db => db.UseSqlite(_connection));
            services.UseEntities<UnfilteredContext>(o => { o.UseDefaults(); Unwired(o); Logging()(o); }).For<Doc>();
        });

#if NET10_0_OR_GREATER
        Assert.That(errors, Has.Some.Contains(MissingFilterError).And.Some.Contains(nameof(Doc)));
        Assert.That(errors, Has.Some.Contains("SetArchivedQueryFilter()"), "the message must carry the fix");
#else
        Assert.That(errors, Has.None.Contains(MissingFilterError),
            "EF Core 9 installs no archived query filter — reporting its absence would fire on every app");
#endif
    }

#if NET10_0_OR_GREATER
    [Test]
    public void A_Missing_Archived_Query_Filter_Stops_The_Host_By_Default()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<UnfilteredContext>(db => db.UseSqlite(_connection));
        // ThrowOnError defaults to true: a silent soft-delete leak is a hard gate, not a warning
        services.UseEntities<UnfilteredContext>(o =>
            {
                o.UseDefaults();
                Unwired(o);
                o.ConfigureValidation(v => v.Enabled = true);
            })
            .For<Doc>();

        using var sp = services.BuildServiceProvider();
        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => RunHostedServices(sp));
        Assert.That(ex!.Message, Does.Contain(MissingFilterError));
    }
#endif

    // ── false-positive guards ──────────────────────────────────────────────────

    [Test]
    public async Task The_Default_Wiring_Is_Not_Reported()
    {
        // The same DbContext as the defect above, minus the wiring opt-out: UseDefaults() installs the filter
        // through the context's options, so a consumer DbContext needs no soft-delete plumbing of its own.
        var errors = await Errors(services =>
        {
            services.AddDbContext<UnfilteredContext>(db => db.UseSqlite(_connection));
            services.UseEntities<UnfilteredContext>(o => { o.UseDefaults(); Logging()(o); }).For<Doc>();
        });

        Assert.That(errors, Has.None.Contains(MissingFilterError));
    }

    [Test]
    public async Task Calling_SetArchivedQueryFilter_Is_Not_Reported()
    {
        var errors = await Errors(services =>
        {
            services.AddDbContext<FilteredContext>(db => db.UseSqlite(_connection));
            services.UseEntities<FilteredContext>(o => { o.UseDefaults(); Logging()(o); }).For<Doc>();
        });

        Assert.That(errors, Has.None.Contains(MissingFilterError));
    }

    [Test]
    public async Task A_Guid_Keyed_Archivable_Entity_Is_Not_Reported()
    {
        var errors = await Errors(services =>
        {
            services.AddDbContext<GuidContext>(db => db.UseSqlite(_connection));
            services.UseEntities<GuidContext>(o => { o.UseDefaults(); Logging()(o); }).For<GuidDoc, Guid>();
        });

        Assert.That(errors, Has.None.Contains(MissingFilterError),
            "the filter is key-agnostic — a non-int key must not read as unprotected");
    }

    [Test]
    public async Task An_App_With_Attachments_Is_Not_Reported()
    {
        var errors = await Errors(services =>
        {
            services.AddDbContext<AttachmentContext>(db => db.UseSqlite(_connection));
            services.UseEntities<AttachmentContext>(o => { o.UseDefaults(); Logging()(o); })
                .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = Path.GetTempPath() }))
                .For<Folder>(e => e.HasAttachments(x => x.Attachments));
        });

        Assert.That(errors, Has.None.Contains(MissingFilterError),
            "the entity types HasAttachments() adds are not IArchivable and must not be reported");
    }

    [Test]
    public async Task A_Derived_Entity_Type_Is_Not_Reported()
    {
        var errors = await Errors(services =>
        {
            services.AddDbContext<HierarchyContext>(db => db.UseSqlite(_connection));
            services.UseEntities<HierarchyContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Record>()
                .For<SpecialRecord>();
        });

        Assert.That(errors, Has.None.Contains(MissingFilterError),
            "EF takes the filter on the root of a hierarchy, which already covers the derived types");
    }

    [Test]
    public async Task An_App_Without_Archivable_Entities_Is_Not_Reported()
    {
        var errors = await Errors(services =>
        {
            services.AddDbContext<PlainContext>(db => db.UseSqlite(_connection));
            services.UseEntities<PlainContext>(o => { o.UseDefaults(); Logging()(o); }).For<Plain>();
        });

        Assert.That(errors, Has.None.Contains(MissingFilterError));
    }

    [Test]
    public async Task An_Explicit_Included_Default_Suppresses_The_Report()
    {
        // Returning archived rows by default is a legitimate configuration: the app said so out loud, so a
        // missing filter is its choice rather than a defect.
        var errors = await Errors(services =>
        {
            services.AddDbContext<UnfilteredContext>(db => db.UseSqlite(_connection));
            services.UseEntities<UnfilteredContext>(o =>
                {
                    o.UseDefaults();
                    Unwired(o);
                    o.DefaultArchivedFilter = ArchivedFilter.Included;
                    Logging()(o);
                })
                .For<Doc>();
        });

        Assert.That(errors, Has.None.Contains(MissingFilterError));
    }

    [Test]
    public async Task Validation_Off_Outside_Development_Keeps_The_Host_Up()
    {
        // the defect, with no ConfigureValidation call: Enabled is null and no IHostEnvironment is present
        var errors = await Errors(services =>
        {
            services.AddDbContext<UnfilteredContext>(db => db.UseSqlite(_connection));
            services.UseEntities<UnfilteredContext>(o => { o.UseDefaults(); Unwired(o); }).For<Doc>();
        });

        Assert.That(errors, Is.Empty);
    }
}
