using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing;

/// <summary>
/// The startup gate for an attachments owner whose input DTO cannot carry the collection. The attachments
/// sync treats a null incoming collection as "not sent" — correct for a client that omits it, but when the
/// DTO has no <c>Attachments</c> property at all, the convention map makes "sent" impossible: adds, removes
/// and reorders in the entity payload are ignored with a 200 OK and no log, while the
/// <c>/{id}/attachments</c> sub-routes keep working and mask the gap.
/// </summary>
[TestFixture]
public class AttachmentsInputDtoValidatorTests
{
    private const string Hazard = "has no Attachments collection";

    public class Document : IEntity<int>, IHasAttachments
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public ICollection<IEntityAttachment>? Attachments { get; set; }
        public bool? HasAttachment { get; set; }
    }

    /// <summary>Implements only the typed interface — the shape the worked examples use.</summary>
    public class Report : IEntity<int>, IHasAttachments<EntityAttachment>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public ICollection<EntityAttachment>? Attachments { get; set; }
        public bool? HasAttachment { get; set; }
    }

    /// <summary>No attachments at all — the negative control.</summary>
    public class Note : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    public record DocumentDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    /// <summary>The hazard: an input DTO without the collection.</summary>
    public record BareInputDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    /// <summary>The documented shape.</summary>
    public record CompleteInputDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public ICollection<EntityAttachmentInputDto>? Attachments { get; set; }
    }

    public class DocumentContext(DbContextOptions<DocumentContext> options) : DbContext(options)
    {
        public DbSet<Document> Documents => Set<Document>();
        public DbSet<Note> Notes => Set<Note>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Document>().Ignore(x => x.Attachments);
        }
    }

    public class ReportContext(DbContextOptions<ReportContext> options) : DbContext(options)
    {
        public DbSet<Report> Reports => Set<Report>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Report>().Ignore(x => x.Attachments);
        }
    }

    private sealed class CaptureLoggerProvider : ILoggerProvider
    {
        public List<string> Warnings { get; } = [];
        public ILogger CreateLogger(string categoryName) => new CaptureLogger(this);
        public void Dispose() { }

        private sealed class CaptureLogger(CaptureLoggerProvider provider) : ILogger
        {
            public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;
            public bool IsEnabled(LogLevel logLevel) => true;
            public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter)
            {
                if (logLevel == LogLevel.Warning) provider.Warnings.Add(formatter(state, exception));
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

    private async Task<List<string>> Warnings(Action<IServiceCollection> configure)
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        configure(services);

        await using var sp = services.BuildServiceProvider();
        foreach (var hostedService in sp.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
        return capture.Warnings;
    }

    private static Action<Regira.Entities.DependencyInjection.ServiceCollections.Models.EntityServiceCollectionOptions> Logging()
        => o => o.ConfigureValidation(v =>
        {
            v.Enabled = true;
            v.ThrowOnError = false;
        });

    // ── the hazard ─────────────────────────────────────────────────────────────

    [Test]
    public async Task An_Owner_Whose_Input_Dto_Lacks_The_Collection_Is_Reported()
    {
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<DocumentContext>(db => db.UseSqlite(_connection));
            services.UseEntities<DocumentContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Document>();
            // what UseMapping<DocumentDto, BareInputDto>() records — registered directly to keep the
            // fixture mapper-agnostic; the validator reads the descriptor, not the mapper
            services.AddSingleton(new EntityMappingRegistration(typeof(Document), typeof(DocumentDto), typeof(BareInputDto)));
        });

        Assert.Multiple(() =>
        {
            Assert.That(warnings, Has.Some.Contains(Hazard).And.Some.Contains(nameof(Document)).And.Some.Contains(nameof(BareInputDto)));
            Assert.That(warnings, Has.Some.Contains("silently ignored"), "the message must carry the symptom");
            Assert.That(warnings, Has.Some.Contains("ICollection<EntityAttachmentInputDto>"), "the message must carry the one-line remedy");
        });
    }

    [Test]
    public async Task A_Typed_Only_Owner_Is_Reported_Too()
    {
        // IHasAttachments<T> does not extend the non-generic marker, so the probe must catch both shapes.
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<ReportContext>(db => db.UseSqlite(_connection));
            services.UseEntities<ReportContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Report>();
            services.AddSingleton(new EntityMappingRegistration(typeof(Report), typeof(DocumentDto), typeof(BareInputDto)));
        });

        Assert.That(warnings, Has.Some.Contains(Hazard).And.Some.Contains(nameof(Report)));
    }

    // ── false-positive guards ──────────────────────────────────────────────────

    [Test]
    public async Task An_Owner_Whose_Input_Dto_Declares_The_Collection_Is_Not_Reported()
    {
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<DocumentContext>(db => db.UseSqlite(_connection));
            services.UseEntities<DocumentContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Document>();
            services.AddSingleton(new EntityMappingRegistration(typeof(Document), typeof(DocumentDto), typeof(CompleteInputDto)));
        });

        Assert.That(warnings, Has.None.Contains(Hazard));
    }

    [Test]
    public async Task An_Unmapped_Owner_Is_Not_Reported()
    {
        // Entity-as-DTO: the write path binds the entity itself, where the collection always exists.
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<DocumentContext>(db => db.UseSqlite(_connection));
            services.UseEntities<DocumentContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Document>();
        });

        Assert.That(warnings, Has.None.Contains(Hazard));
    }

    [Test]
    public async Task A_Non_Owner_With_A_Bare_Input_Dto_Is_Not_Reported()
    {
        var warnings = await Warnings(services =>
        {
            services.AddDbContext<DocumentContext>(db => db.UseSqlite(_connection));
            services.UseEntities<DocumentContext>(o => { o.UseDefaults(); Logging()(o); })
                .For<Note>();
            services.AddSingleton(new EntityMappingRegistration(typeof(Note), typeof(DocumentDto), typeof(BareInputDto)));
        });

        Assert.That(warnings, Has.None.Contains(Hazard));
    }
}
