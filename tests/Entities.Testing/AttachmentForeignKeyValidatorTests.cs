using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing;

/// <summary>
/// The startup gate for an attachments owner whose collection is left to EF's conventions. <c>ObjectId</c> is
/// not a conventional foreign-key name and the link has no navigation back to its owner, so EF invents a shadow
/// <c>ProductId</c>; the pipeline writes <c>ObjectId</c>, and every link row is saved orphaned — the owner's
/// <c>Attachments</c> loads empty with no error anywhere.
/// </summary>
[TestFixture]
public class AttachmentForeignKeyValidatorTests
{
    private const string Hazard = "not ObjectId";

    public class ProductAttachment : EntityAttachment
    {
        public ProductAttachment() => ObjectType = nameof(Product);
    }

    public class Product : IEntityWithSerial, IHasAttachments, IHasAttachments<ProductAttachment>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool? HasAttachment { get; set; }
        public ICollection<ProductAttachment>? Attachments { get; set; }
        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<ProductAttachment>().ToArray();
        }
    }

    /// <summary>The hazard: only the link's own <c>Attachment</c> FK is configured.</summary>
    public class ConventionContext(DbContextOptions<ConventionContext> options) : DbContext(options)
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductAttachment> ProductAttachments => Set<ProductAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Product>().Ignore(x => x.HasAttachment);
            modelBuilder.Entity<ProductAttachment>().HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
        }
    }

    /// <summary>The documented configuration.</summary>
    public class MappedContext(DbContextOptions<MappedContext> options) : DbContext(options)
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductAttachment> ProductAttachments => Set<ProductAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Product>().Ignore(x => x.HasAttachment);
            modelBuilder.Entity<ProductAttachment>().HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
            modelBuilder.Entity<Product>().HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id);
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

    private async Task<List<string>> Warnings<TContext>()
        where TContext : DbContext
    {
        var capture = new CaptureLoggerProvider();
        var services = new ServiceCollection();
        services.AddLogging(b => b.AddProvider(capture));
        services.AddDbContext<TContext>(db => db.UseSqlite(_connection));
        services.UseEntities<TContext>(o =>
            {
                o.UseDefaults();
                o.ConfigureValidation(v =>
                {
                    v.Enabled = true;
                    v.ThrowOnError = false;
                });
            })
            .For<Product>();

        await using var sp = services.BuildServiceProvider();
        foreach (var hostedService in sp.GetServices<IHostedService>())
        {
            await hostedService.StartAsync(CancellationToken.None);
        }
        return capture.Warnings;
    }

    [Test]
    public async Task Owner_Collection_Left_To_Conventions_Is_Reported()
    {
        var warnings = await Warnings<ConventionContext>();

        Assert.Multiple(() =>
        {
            Assert.That(warnings, Has.Some.Contains(Hazard).And.Some.Contains("Product.Attachments"));
            Assert.That(warnings, Has.Some.Contains("ProductId (shadow)"), "the message must name the key EF invented");
            Assert.That(warnings, Has.Some.Contains("HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId)"),
                "the message must carry the mapping that fixes it");
        });
    }

    [Test]
    public async Task Owner_Collection_Mapped_To_ObjectId_Is_Not_Reported()
    {
        var warnings = await Warnings<MappedContext>();

        Assert.That(warnings, Has.None.Contains(Hazard));
    }

    /// <summary>Why the warning exists: the same link row, saved by ObjectId, under both mappings.</summary>
    [Test]
    public async Task Link_Rows_Saved_By_ObjectId_Only_Load_Under_The_ObjectId_Mapping()
    {
        var byConvention = await LoadedLinkCount(new ConventionContext(Options<ConventionContext>()));
        var byObjectId = await LoadedLinkCount(new MappedContext(Options<MappedContext>()));

        Assert.Multiple(() =>
        {
            Assert.That(byConvention, Is.Zero, "the row is saved, but its shadow key is null");
            Assert.That(byObjectId, Is.EqualTo(1));
        });
    }

    private DbContextOptions<TContext> Options<TContext>() where TContext : DbContext
        => new DbContextOptionsBuilder<TContext>().UseSqlite(_connection).Options;

    private static async Task<int> LoadedLinkCount(DbContext db)
    {
        await using var _ = db;
        await db.Database.EnsureDeletedAsync();
        await db.Database.EnsureCreatedAsync();

        var product = new Product { Title = "Desk" };
        db.Add(product);
        await db.SaveChangesAsync();
        db.Add(new ProductAttachment
        {
            ObjectId = product.Id,
            Attachment = new Attachment { FileName = "spec.pdf", ContentType = "application/pdf" }
        });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();

        var loaded = await db.Set<Product>().Include(x => x.Attachments).SingleAsync();
        return loaded.Attachments?.Count ?? 0;
    }
}
