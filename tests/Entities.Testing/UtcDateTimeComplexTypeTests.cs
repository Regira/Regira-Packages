using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Regira.DAL.EFcore.Extensions;

namespace Entities.Testing;

/// <summary>
/// A <c>DateTime</c> inside a complex type (<c>ComplexProperty</c> value object) is not a property of the entity type,
/// and the UTC convention used to walk only the entity's own: the value was read back Unspecified and served without
/// the <c>Z</c>, while <c>Created</c> on the same row was fine. Every wiring of the convention has to reach it,
/// nested value objects included.
/// </summary>
[TestFixture]
public class UtcDateTimeComplexTypeTests
{
    public class Visit
    {
        public int Id { get; set; }
        public DateTime Created { get; set; }
        public Patient Patient { get; set; } = new();
    }

    public class Patient
    {
        public string? Name { get; set; }
        public DateTime? FetchedAt { get; set; }
        public Consent Consent { get; set; } = new();
    }

    public class Consent
    {
        public DateTime SignedAt { get; set; }
    }

    private static void MapVisit(ModelBuilder modelBuilder)
        => modelBuilder.Entity<Visit>().ComplexProperty(x => x.Patient, p => p.ComplexProperty(x => x.Consent));

    public class OptionsWiredContext(DbContextOptions<OptionsWiredContext> options) : DbContext(options)
    {
        public DbSet<Visit> Visits => Set<Visit>();
        protected override void OnModelCreating(ModelBuilder modelBuilder) => MapVisit(modelBuilder);
    }

    public class ModelBuilderWiredContext(DbContextOptions<ModelBuilderWiredContext> options) : DbContext(options)
    {
        public DbSet<Visit> Visits => Set<Visit>();
        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            MapVisit(modelBuilder);
            modelBuilder.SetUtcDateTimeConvention();
        }
    }

    public class ConventionsWiredContext(DbContextOptions<ConventionsWiredContext> options) : DbContext(options)
    {
        public DbSet<Visit> Visits => Set<Visit>();
        protected override void ConfigureConventions(ModelConfigurationBuilder configurationBuilder) => configurationBuilder.SetUtcDateTimeConvention();
        protected override void OnModelCreating(ModelBuilder modelBuilder) => MapVisit(modelBuilder);
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

    private TContext Create<TContext>(bool viaOptions) where TContext : DbContext
    {
        var options = new DbContextOptionsBuilder<TContext>().UseSqlite(_connection);
        if (viaOptions)
        {
            options.AddUtcDateTimeConvention();
        }
        return (TContext)Activator.CreateInstance(typeof(TContext), options.Options)!;
    }

    private async Task<Visit> RoundTrip<TContext>(bool viaOptions) where TContext : DbContext
    {
        var instant = new DateTime(2026, 9, 23, 8, 32, 32, DateTimeKind.Utc);
        await using (var db = Create<TContext>(viaOptions))
        {
            await db.Database.EnsureCreatedAsync();
            db.Set<Visit>().Add(new Visit
            {
                Id = 1,
                Created = instant,
                // local kind: the write side must normalize it to the same instant
                Patient = new Patient { Name = "P", FetchedAt = instant.ToLocalTime(), Consent = new Consent { SignedAt = instant } },
            });
            await db.SaveChangesAsync();
        }

        await using var read = Create<TContext>(viaOptions);
        return await read.Set<Visit>().AsNoTracking().SingleAsync();
    }

    private static void AssertUtc(Visit visit)
    {
        var instant = new DateTime(2026, 9, 23, 8, 32, 32, DateTimeKind.Utc);
        Assert.Multiple(() =>
        {
            Assert.That(visit.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(visit.Patient.FetchedAt!.Value.Kind, Is.EqualTo(DateTimeKind.Utc), "a value object's DateTime");
            Assert.That(visit.Patient.FetchedAt, Is.EqualTo(instant));
            Assert.That(visit.Patient.Consent.SignedAt.Kind, Is.EqualTo(DateTimeKind.Utc), "a nested value object's DateTime");
        });
    }

    [Test]
    public async Task The_Options_Convention_Reaches_Complex_Types()
        => AssertUtc(await RoundTrip<OptionsWiredContext>(viaOptions: true));

    [Test]
    public async Task The_ModelBuilder_Convention_Reaches_Complex_Types()
        => AssertUtc(await RoundTrip<ModelBuilderWiredContext>(viaOptions: false));

    [Test]
    public async Task The_ConfigureConventions_Variant_Reaches_Complex_Types()
        => AssertUtc(await RoundTrip<ConventionsWiredContext>(viaOptions: false));
}
