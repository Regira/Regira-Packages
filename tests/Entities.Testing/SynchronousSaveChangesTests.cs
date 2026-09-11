using System.ComponentModel.DataAnnotations;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Normalizing;

namespace Entities.Testing;

/// <summary>
/// The primer, normalizer and auto-truncate interceptors on the <b>synchronous</b> <c>SaveChanges()</c> — the call a
/// seeder, a job or a context's own <c>SaveChanges</c> override makes. Every behaviour is pinned on both calls, so the
/// asynchronous path is shown to be unchanged. The last group pins what makes waiting on an asynchronous primer from
/// the synchronous call safe: a primer that really awaits must deadlock neither a thread whose synchronization context
/// cannot run continuations while it is blocked, nor a single-lane task scheduler. Each of those has a control proving
/// the fixture reproduces the hazard, so a guard that never engaged cannot pass for one that works.
/// </summary>
[TestFixture]
public class SynchronousSaveChangesTests
{
    public class Category : IEntityWithSerial, IHasTimestamps, IHasTitle, IHasNormalizedContent, IArchivable
    {
        public int Id { get; set; }
        [Required, MaxLength(64)] public string Title { get; set; } = null!;
        [MaxLength(1024)] public string? Description { get; set; }
        [MaxLength(1024), Normalized(SourceProperties = [nameof(Title), nameof(Description)])]
        public string? NormalizedContent { get; set; }
        public bool IsArchived { get; set; }
        public DateTime Created { get; set; }
        public DateTime? LastModified { get; set; }
    }

    public class CatalogContext(DbContextOptions<CatalogContext> options) : DbContext(options)
    {
        public DbSet<Category> Categories => Set<Category>();
    }

    private const string AfterAwait = "set after the primer's await";

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private int _primed;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _primed = 0;

        var services = new ServiceCollection();
        services.AddDbContext<CatalogContext>(db => db.UseSqlite(_connection));
        services.UseEntities<CatalogContext>(o => o.UseDefaults())
            .For<Category>(e => e.Prime(_ => _primed++));
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        scope.ServiceProvider.GetRequiredService<CatalogContext>().Database.EnsureCreated();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    private static Task Save(DbContext db, bool synchronous)
    {
        if (synchronous)
        {
            db.SaveChanges();
            return Task.CompletedTask;
        }
        return db.SaveChangesAsync();
    }

    private async Task<int> Seed(Category category)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        db.Categories.Add(category);
        await db.SaveChangesAsync();
        _primed = 0;
        return category.Id;
    }

    // ── primers ────────────────────────────────────────────────────────────────

    [TestCase(true)]
    [TestCase(false)]
    public async Task Removing_An_Archivable_Archives_It(bool synchronous)
    {
        var id = await Seed(new Category { Title = "Books" });

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
            db.Categories.Remove(await db.Categories.SingleAsync(x => x.Id == id));
            await Save(db, synchronous);
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
            var stored = await db.Categories.IgnoreQueryFilters().AsNoTracking().SingleOrDefaultAsync(x => x.Id == id);
            Assert.That(stored, Is.Not.Null, "the row was deleted instead of archived");
            Assert.That(stored!.IsArchived, Is.True);
        }
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task An_Insert_Stamps_Created(bool synchronous)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        var category = new Category { Title = "Books" };
        db.Categories.Add(category);

        await Save(db, synchronous);

        Assert.Multiple(() =>
        {
            Assert.That(category.Created, Is.Not.EqualTo(DateTime.MinValue));
            Assert.That(category.Created.Kind, Is.EqualTo(DateTimeKind.Utc));
            Assert.That(category.LastModified, Is.Null);
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task An_Update_Stamps_LastModified_And_Restores_Created(bool synchronous)
    {
        var created = new DateTime(2020, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var id = await Seed(new Category { Title = "Books", Created = created });

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        var category = await db.Categories.SingleAsync(x => x.Id == id);
        category.Title = "Books & Media";
        category.Created = default; // what an entity mapped from an input DTO without Created carries

        await Save(db, synchronous);

        Assert.Multiple(() =>
        {
            Assert.That(category.Created, Is.EqualTo(created));
            Assert.That(category.LastModified, Is.GreaterThan(created));
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Custom_Primer_Runs_Once(bool synchronous)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        db.Categories.Add(new Category { Title = "Books" });

        await Save(db, synchronous);

        Assert.That(_primed, Is.EqualTo(1));
    }

    // ── normalizers and auto-truncate ──────────────────────────────────────────

    [TestCase(true)]
    [TestCase(false)]
    public async Task The_Normalizer_Fills_NormalizedContent(bool synchronous)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        var category = new Category { Title = "Books", Description = "Novels and poetry" };
        db.Categories.Add(category);

        await Save(db, synchronous);

        Assert.That(category.NormalizedContent, Is.Not.Null.And.Not.Empty);
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task Auto_Truncate_Clips_A_String_To_Its_MaxLength(bool synchronous)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        var category = new Category { Title = new string('x', 80) };
        db.Categories.Add(category);

        await Save(db, synchronous);

        Assert.That(category.Title, Has.Length.EqualTo(64));
    }

    // ── waiting on an awaiting primer from the synchronous call ────────────────

    /// <summary>
    /// Counts posts and never runs them: the thread that would run them is the one blocked in <c>SaveChanges()</c>,
    /// which is what a UI thread or a single-threaded async context amounts to while it waits.
    /// </summary>
    private sealed class BlockedSynchronizationContext : SynchronizationContext
    {
        private int _posts;
        public int Posts => Volatile.Read(ref _posts);
        public override void Post(SendOrPostCallback d, object? state) => Interlocked.Increment(ref _posts);
        public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();
        public override SynchronizationContext CreateCopy() => this;
    }

    private ServiceProvider BuildWithAwaitingPrimer()
    {
        var services = new ServiceCollection();
        services.AddDbContext<CatalogContext>(db => db.UseSqlite(_connection));
        services.UseEntities<CatalogContext>(o => o.UseDefaults())
            .For<Category>(e => e.Prime(async (category, _, _) =>
            {
                await Task.Yield();
                category.Description = AfterAwait;
            }));
        return services.BuildServiceProvider();
    }

    /// <summary>
    /// Runs the action on a dedicated thread carrying <paramref name="context"/> and reports whether it finished in
    /// time. A deadlocked thread stays blocked; as a background thread it cannot keep the test host alive.
    /// </summary>
    private static bool RunOnThreadWith(SynchronizationContext context, Action action, out Exception? error)
    {
        Exception? caught = null;
        var thread = new Thread(() =>
        {
            SynchronizationContext.SetSynchronizationContext(context);
            try
            {
                action();
            }
            catch (Exception ex)
            {
                caught = ex;
            }
        }) { IsBackground = true };
        thread.Start();
        var completed = thread.Join(TimeSpan.FromSeconds(10));
        error = caught;
        return completed;
    }

    [Test]
    public void Control_A_Blocked_Context_Captures_The_Continuation_Of_An_Await()
    {
        var context = new BlockedSynchronizationContext();
        Task? pending = null;

        Assert.That(RunOnThreadWith(context, () => pending = YieldAsync(), out _), Is.True);
        Assert.Multiple(() =>
        {
            // blocking on `pending` from that thread would therefore never return
            Assert.That(context.Posts, Is.EqualTo(1));
            Assert.That(pending!.IsCompleted, Is.False);
        });

        static async Task YieldAsync() => await Task.Yield();
    }

    [Test]
    public void A_Synchronous_Save_Under_A_Blocked_Context_Waits_For_An_Awaiting_Primer()
    {
        using var sp = BuildWithAwaitingPrimer();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        var category = new Category { Title = "Books" };
        db.Categories.Add(category);
        var context = new BlockedSynchronizationContext();

        var completed = RunOnThreadWith(context, () => db.SaveChanges(), out var error);

        Assert.Multiple(() =>
        {
            Assert.That(completed, Is.True, "the synchronous save deadlocked on the primer's continuation");
            Assert.That(error, Is.Null);
            Assert.That(context.Posts, Is.Zero);
        });
        Assert.That(db.Categories.AsNoTracking().Single(x => x.Id == category.Id).Description, Is.EqualTo(AfterAwait));
    }

    [Test]
    public void Control_An_Exclusive_Scheduler_Takes_The_Continuation_Of_An_Await()
    {
        var scheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

        var resumedOn = Task.Factory.StartNew(ResumeAsync, CancellationToken.None, TaskCreationOptions.None, scheduler).Unwrap();

        // a caller blocked on that one lane would therefore wait for a continuation queued behind itself
        Assert.That(resumedOn.Wait(TimeSpan.FromSeconds(10)), Is.True);
        Assert.That(resumedOn.Result, Is.SameAs(scheduler));

        static async Task<TaskScheduler> ResumeAsync()
        {
            await Task.Yield();
            return TaskScheduler.Current;
        }
    }

    [Test]
    public void A_Synchronous_Save_On_An_Exclusive_Scheduler_Waits_For_An_Awaiting_Primer()
    {
        using var sp = BuildWithAwaitingPrimer();
        using var scope = sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<CatalogContext>();
        var category = new Category { Title = "Books" };
        db.Categories.Add(category);
        var scheduler = new ConcurrentExclusiveSchedulerPair().ExclusiveScheduler;

        var save = Task.Factory.StartNew(() => db.SaveChanges(), CancellationToken.None, TaskCreationOptions.None, scheduler);

        Assert.That(save.Wait(TimeSpan.FromSeconds(10)), Is.True, "the synchronous save deadlocked on the primer's continuation");
        Assert.That(db.Categories.AsNoTracking().Single(x => x.Id == category.Id).Description, Is.EqualTo(AfterAwait));
    }
}
