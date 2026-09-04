using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.ComponentModel.DataAnnotations;

namespace Entities.Testing;

/// <summary>
/// An entity that carries a foreign key to one of its own children makes the two rows reference each other,
/// and EF Core cannot order the two deletes. The delete is a 500 on every such entity until the reference is
/// dropped in a save of its own.
/// </summary>
[TestFixture]
public class DeleteCycleTests
{
    public class Article : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        /// The marked child — optional, which is what makes the cycle breakable.
        public int? CoverImageId { get; set; }
        public ICollection<ArticleImage>? Images { get; set; }
        /// A store-generated token, moved by a trigger on every UPDATE — what a SQL Server rowversion does.
        public int RowVersion { get; set; }
        /// An application-owned token: only the application writes it, so a changed value means another writer.
        [ConcurrencyCheck]
        public int Version { get; set; }
    }

    /// The child: its foreign key back at the owner is required, and therefore cascades.
    public class ArticleImage : IEntity<int>
    {
        public int Id { get; set; }
        public int ArticleId { get; set; }
        public string? FileName { get; set; }
    }

    public class ArticleContext(DbContextOptions<ArticleContext> options) : DbContext(options)
    {
        /// <summary>Off by default so the same fixture can show the failure and the fix.</summary>
        public bool BreakDeleteCycles { get; set; }
        /// <summary>How often the real save ran — one round trip unless a cycle had to be broken.</summary>
        public int SaveCount { get; private set; }

        public DbSet<Article> Articles { get; set; } = null!;
        public DbSet<ArticleImage> ArticleImages { get; set; } = null!;

        protected override void OnModelCreating(ModelBuilder builder)
            => builder.Entity<Article>(entity =>
            {
                entity.HasMany(x => x.Images).WithOne()
                    .HasForeignKey(a => a.ArticleId).HasPrincipalKey(x => x.Id);
                // ClientSetNull, not SetNull: two cascade paths between the same pair of tables is what
                // SQL Server rejects at migration time with "may cause cycles or multiple cascade paths".
                entity.HasOne<ArticleImage>().WithMany()
                    .HasForeignKey(x => x.CoverImageId)
                    .OnDelete(DeleteBehavior.ClientSetNull);
                // SQLite has no rowversion; the trigger created in Seed plays its part on every UPDATE.
                entity.Property(x => x.RowVersion).HasDefaultValue(1).ValueGeneratedOnAddOrUpdate().IsConcurrencyToken();
            });

        public override int SaveChanges(bool acceptAllChangesOnSuccess)
            => BreakDeleteCycles
                ? this.SaveChangesBreakingDeleteCycles(CountedSave, acceptAllChangesOnSuccess)
                : CountedSave(acceptAllChangesOnSuccess);

        public override Task<int> SaveChangesAsync(bool acceptAllChangesOnSuccess, CancellationToken token = default)
            => BreakDeleteCycles
                ? this.SaveChangesBreakingDeleteCyclesAsync(CountedSaveAsync, acceptAllChangesOnSuccess, token)
                : CountedSaveAsync(acceptAllChangesOnSuccess, token);

        /// <summary>
        /// Simulates the database failing the save that carries the owner's DELETE — the round trip after the
        /// reference has already been dropped — which is the one moment the change tracker and the database
        /// can disagree about what was persisted.
        /// </summary>
        public bool FailTheSaveThatDeletesTheArticle { get; set; }

        private int CountedSave(bool acceptAllChangesOnSuccess)
        {
            SaveCount++;
            ThrowIfSimulatingFailure();
            return base.SaveChanges(acceptAllChangesOnSuccess);
        }
        private Task<int> CountedSaveAsync(bool acceptAllChangesOnSuccess, CancellationToken token)
        {
            SaveCount++;
            ThrowIfSimulatingFailure();
            return base.SaveChangesAsync(acceptAllChangesOnSuccess, token);
        }
        private void ThrowIfSimulatingFailure()
        {
            if (FailTheSaveThatDeletesTheArticle && ChangeTracker.Entries<Article>().Any(e => e.State == EntityState.Deleted))
                throw new InvalidOperationException("simulated: the database rejected the save carrying the DELETE");
        }
    }


    /// <summary>
    /// What <c>EnableRetryOnFailure()</c> installs on SQL Server, which SQLite has no equivalent of. Nothing
    /// here has to actually retry — an execution strategy that <b>might</b> is already enough for EF to refuse
    /// a transaction it did not start itself, unless it runs inside another strategy, which suspends the check.
    /// </summary>
    private sealed class RetryingStrategy(ExecutionStrategyDependencies dependencies)
        : ExecutionStrategy(dependencies, maxRetryCount: 3, maxRetryDelay: TimeSpan.Zero)
    {
        protected override bool ShouldRetryOn(Exception exception) => false;
    }


    private SqliteConnection _connection = null!;
    private ServiceProvider _serviceProvider = null!;

    [SetUp]
    public void Setup() => Setup(retryOnFailure: false);

    private void Setup(bool retryOnFailure)
    {
        _serviceProvider?.Dispose();
        _connection?.Close();
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ArticleContext>(db => db.UseSqlite(_connection, sqlite =>
        {
            if (retryOnFailure)
            {
                sqlite.ExecutionStrategy(dependencies => new RetryingStrategy(dependencies));
            }
        }));
        services.UseEntities<ArticleContext>()
            // eager-loading the children is what puts them in the change tracker, and the delete cycle with them
            .For<Article>(e => e.Includes((query, _) => query.Include(x => x.Images!)));
        _serviceProvider = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _serviceProvider.Dispose();
        _connection.Close();
    }


    [Test]
    public async Task Deleting_An_Entity_That_References_Its_Own_Child_Is_Refused_By_EF()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);

        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.That(ex!.Message, Does.Contain("circular dependency"),
            "the fixture only reproduces the trap while both rows are deleted together");
    }

    [Test]
    public async Task Nulling_The_Foreign_Key_On_The_Deleted_Entry_Does_Not_Help()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);

        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        // What a primer or prepper would do — they do run for deleted entries. EF builds the delete order
        // from the ORIGINAL values, so the current value it sets is never read and the save fails identically.
        db.Entry(article).Property(x => x.CoverImageId).CurrentValue = null;

        var ex = Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        Assert.That(ex!.Message, Does.Contain("circular dependency"));
    }

    [Test]
    public async Task Faking_The_Original_Value_Only_Moves_The_Failure_To_The_Database()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);

        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        // The next thing to try after the primer: lie about the original value so EF sees no edge. The delete
        // order comes out fine and the database rejects it — the stored row still holds the reference while
        // the child it points at is deleted. Two round trips is not an implementation choice.
        db.Entry(article).Property(x => x.CoverImageId).OriginalValue = null;

        var ex = Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        Assert.That(ex!.InnerException?.Message, Does.Contain("FOREIGN KEY constraint failed"));
    }

    [Test]
    public async Task Breaking_The_Cycle_Deletes_The_Entity_And_Its_Children()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;
        var before = db.SaveCount;

        // through the framework's own write path — IEntityService.Remove + SaveChanges, what DELETE /{id} calls
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Article, int>>();
        var article = await service.Details(id);
        await service.Remove(article!);
        await service.SaveChanges();

        Assert.Multiple(async () =>
        {
            Assert.That(await db.Articles.CountAsync(), Is.Zero);
            Assert.That(await db.ArticleImages.CountAsync(), Is.Zero, "the children go with the owner");
            Assert.That(db.SaveCount - before, Is.EqualTo(1), "the reference is dropped by a direct UPDATE, then a single save carries the deletes");
        });
    }

    [Test]
    public async Task Breaking_The_Cycle_Works_On_The_Synchronous_Path()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        var article = db.Articles.Include(x => x.Images!).First(x => x.Id == id);
        db.Articles.Remove(article);
        db.SaveChanges();

        Assert.Multiple(() =>
        {
            Assert.That(db.Articles.Count(), Is.Zero);
            Assert.That(db.ArticleImages.Count(), Is.Zero);
        });
    }

    [Test]
    public async Task Breaking_The_Cycle_Survives_A_Deferred_Cascade()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;
        // the children are still Unchanged when Remove returns, so the cycle only exists once EF cascades
        db.ChangeTracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;

        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        await db.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await db.Articles.CountAsync(), Is.Zero);
            Assert.That(await db.ArticleImages.CountAsync(), Is.Zero);
        });
    }

    [Test]
    public async Task A_Save_Without_A_Cycle_Stays_A_Single_Round_Trip()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;
        var before = db.SaveCount;

        // an entity deleted without its children loaded has no edge to another pending delete
        var article = await db.Articles.FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        await db.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(db.SaveCount - before, Is.EqualTo(1));
            Assert.That(await db.Articles.CountAsync(), Is.Zero);
            Assert.That(await db.ArticleImages.CountAsync(), Is.Zero, "the database cascade still reaches them");
        });
    }

    [Test]
    public async Task Deleting_Only_The_Child_Is_Untouched()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;
        var before = db.SaveCount;

        // only one row is deleted, so there is no cycle — EF's own ClientSetNull fixup nulls the reference
        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.ArticleImages.Remove(article.Images!.First());
        await db.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(db.SaveCount - before, Is.EqualTo(1));
            Assert.That(await db.ArticleImages.CountAsync(), Is.Zero);
            Assert.That((await db.Articles.AsNoTracking().FirstAsync()).CoverImageId, Is.Null);
        });
    }


    [Test]
    public async Task Breaking_The_Cycle_Runs_Inside_A_Transaction_The_Caller_Owns()
    {
        // EnableRetryOnFailure() is the standard production setting, and EF's own recipe for combining it with
        // an explicit transaction puts SaveChanges inside a strategy the caller already owns. The helper must
        // not open a transaction of its own in there. (Its nested strategy would be harmless — the outer one
        // suspends the check — but a second transaction is not.)
        Setup(retryOnFailure: true);
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);

        await db.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
        {
            await using var transaction = await db.Database.BeginTransactionAsync();
            await db.SaveChangesAsync();
            await transaction.CommitAsync();
        });

        Assert.Multiple(() =>
        {
            Assert.That(db.Articles.Count(), Is.Zero);
            Assert.That(db.ArticleImages.Count(), Is.Zero);
        });
    }

    [Test]
    public async Task Breaking_The_Cycle_Runs_Inside_A_Transaction_The_Caller_Owns_On_The_Synchronous_Path()
    {
        Setup(retryOnFailure: true);
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        var article = db.Articles.Include(x => x.Images!).First(x => x.Id == id);
        db.Articles.Remove(article);

        db.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = db.Database.BeginTransaction();
            db.SaveChanges();
            transaction.Commit();
        });

        Assert.Multiple(() =>
        {
            Assert.That(db.Articles.Count(), Is.Zero);
            Assert.That(db.ArticleImages.Count(), Is.Zero);
        });
    }

    [Test]
    public async Task A_Bare_Transaction_Under_A_Retrying_Strategy_Is_Refused_By_EF_Itself()
    {
        // The other arrangement: BeginTransaction() with no strategy around it. EF's own SaveChanges refuses it
        // before a statement runs, so the helper skipping its strategy there changes nothing — the same
        // exception with and without a cycle to break. The arrangement is the caller's to fix, and the
        // exception says how.
        Setup(retryOnFailure: true);
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);

        await using var transaction = await db.Database.BeginTransactionAsync();

        var added = db.Articles.Add(new Article { Title = "no cycle" });
        var plain = Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());
        added.State = EntityState.Detached;

        db.BreakDeleteCycles = true;
        db.Articles.Remove(article);
        var broken = Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        Assert.Multiple(() =>
        {
            Assert.That(plain!.Message, Does.Contain("does not support user-initiated transactions"));
            Assert.That(broken!.Message, Is.EqualTo(plain.Message));
        });
    }

    [Test]
    public void A_Strategy_Nested_Inside_The_Callers_Is_Suspended()
    {
        // The premise behind joining the caller's transaction: it is the second transaction the helper must
        // not open, not the strategy. EF refuses a retrying strategy started while a transaction it did not
        // start is current — and suspends one started inside another, so it neither retries nor throws.
        Setup(retryOnFailure: true);
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();

        var nested = db.Database.CreateExecutionStrategy().Execute(() =>
        {
            using var transaction = db.Database.BeginTransaction();
            return db.Database.CreateExecutionStrategy().Execute(() => "ran");
        });
        Assert.That(nested, Is.EqualTo("ran"));

        using var bare = db.Database.BeginTransaction();
        var ex = Assert.Throws<InvalidOperationException>(() => db.Database.CreateExecutionStrategy().Execute(() => "ran"));
        Assert.That(ex!.Message, Does.Contain("does not support user-initiated transactions"));
    }

    [Test]
    public async Task Not_Accepting_Changes_Leaves_The_Deletes_Pending_And_Still_Saves_Them()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        // SaveChanges(false) + AcceptAllChanges() after the commit is EF's own pattern for an externally
        // managed transaction. The reference-dropping UPDATE is accepted regardless — its original values are
        // what the delete order is read from — but the deletes honour the caller's flag.
        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        await db.SaveChangesAsync(acceptAllChangesOnSuccess: false);

        Assert.That(db.Entry(article).State, Is.EqualTo(EntityState.Deleted),
            "the final save honoured the flag, so the caller still decides when the delete is accepted");
        db.ChangeTracker.AcceptAllChanges();

        Assert.Multiple(() =>
        {
            Assert.That(db.Articles.Count(), Is.Zero);
            Assert.That(db.ArticleImages.Count(), Is.Zero);
            Assert.That(db.ChangeTracker.Entries(), Is.Empty);
        });
    }

    [Test]
    public async Task Not_Accepting_Changes_Does_Not_Re_Send_The_Rest_Of_The_Save()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        // An insert riding along in the same save is the second thing an unaccepted phase one would break:
        // still Added when phase two runs, it would be inserted a second time.
        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        db.Articles.Add(new Article { Title = "Survivor" });
        await db.SaveChangesAsync(acceptAllChangesOnSuccess: false);
        db.ChangeTracker.AcceptAllChanges();

        Assert.That(await db.Articles.CountAsync(), Is.EqualTo(1));
    }


    [Test]
    public async Task A_Failed_Delete_Leaves_The_Tracker_Truthful_And_The_Database_Untouched()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        // An unrelated insert rides along in the same save, as it would in any real unit of work. If the round
        // trip carrying the DELETE fails, the transaction rolls the database back — and the tracker must still
        // hold every change as pending, or the next save (EF's retry, or the caller's) silently loses them.
        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        var image = article.Images!.Single();
        var survivor = new Article { Title = "Survivor" };
        db.Articles.Remove(article);
        db.Articles.Add(survivor);
        db.FailTheSaveThatDeletesTheArticle = true;

        Assert.ThrowsAsync<InvalidOperationException>(() => db.SaveChangesAsync());

        Assert.Multiple(async () =>
        {
            Assert.That(db.Entry(survivor).State, Is.EqualTo(EntityState.Added), "the insert was rolled back, so it must still be pending");
            Assert.That(db.Entry(article).State, Is.EqualTo(EntityState.Deleted));
            Assert.That(db.Entry(image).State, Is.EqualTo(EntityState.Deleted));
            Assert.That(await db.Articles.AsNoTracking().CountAsync(), Is.EqualTo(1), "nothing reached the database");
            Assert.That((await db.Articles.AsNoTracking().SingleAsync()).CoverImageId, Is.EqualTo(image.Id), "the dropped reference was rolled back too");
        });

        // The retry a caller (or EF's own strategy) would make must then complete the whole unit of work.
        db.FailTheSaveThatDeletesTheArticle = false;
        await db.SaveChangesAsync();

        Assert.Multiple(async () =>
        {
            Assert.That(await db.Articles.AsNoTracking().Select(x => x.Title).ToListAsync(), Is.EqualTo(new[] { "Survivor" }));
            Assert.That(await db.ArticleImages.CountAsync(), Is.Zero);
        });
    }


    [Test]
    public async Task Breaking_The_Cycle_Counts_Each_Row_Once()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        // The reference-dropping UPDATE is a mechanism, not a change the caller made: the owner and its child
        // are the two rows that change, and SaveChanges reports two — the number EF would report on its own.
        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);

        Assert.That(await db.SaveChangesAsync(), Is.EqualTo(2));
    }


    [Test]
    public async Task A_Store_Generated_Token_Moved_By_The_Reference_Drop_Is_Refreshed()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        // The trigger moves RowVersion on the UPDATE that drops the reference, exactly as a rowversion would.
        // The DELETE's WHERE must carry the value the row holds now; the one loaded before the UPDATE would
        // match nothing and EF would report a concurrency conflict on a row nobody else touched.
        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        var loaded = article.RowVersion;
        db.Articles.Remove(article);

        Assert.DoesNotThrowAsync(() => db.SaveChangesAsync());
        Assert.Multiple(async () =>
        {
            Assert.That(await db.Articles.CountAsync(), Is.Zero);
            Assert.That(loaded, Is.GreaterThan(0), "the fixture's token must have been in play");
        });
    }

    [Test]
    public async Task A_Store_Generated_Token_Still_Detects_Another_Writer()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        // Another writer edits the row after this unit of work loaded it, and the trigger moves RowVersion as
        // a rowversion would. Dropping the reference must fail on that token, like any EF UPDATE would — not
        // overwrite the other writer's row and then adopt the token that writer left behind.
        await db.Database.ExecuteSqlRawAsync("UPDATE Articles SET Title = 'Edited elsewhere' WHERE Id = {0}", id);

        var ex = Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        Assert.That(ex!.Entries.Select(e => e.Entity), Is.EqualTo(new object[] { article }),
            "EF's documented recovery loops ex.Entries; it must find the row that conflicted");
        var row = await db.Articles.AsNoTracking().SingleAsync();
        Assert.Multiple(() =>
        {
            Assert.That(row.CoverImageId, Is.Not.Null, "the reference drop was rolled back");
            Assert.That(row.Title, Is.EqualTo("Edited elsewhere"), "the other writer's change survives");
        });
    }

    [Test]
    public async Task An_Application_Owned_Token_Still_Detects_Another_Writer()
    {
        using var scope = _serviceProvider.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ArticleContext>();
        var id = await Seed(db);
        db.BreakDeleteCycles = true;

        var article = await db.Articles.Include(x => x.Images!).FirstAsync(x => x.Id == id);
        db.Articles.Remove(article);
        // Another writer changes the token the application owns after this unit of work loaded the row. The
        // reference drop never touches that column, so re-reading it from the row would adopt the other
        // writer's value and silently defeat the very check the column exists for.
        await db.Database.ExecuteSqlRawAsync("UPDATE Articles SET Version = Version + 1 WHERE Id = {0}", id);

        var ex = Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => db.SaveChangesAsync());
        Assert.That(ex!.Entries.Select(e => e.Entity), Is.EqualTo(new object[] { article }));
        Assert.That((await db.Articles.AsNoTracking().SingleAsync()).CoverImageId, Is.Not.Null,
            "the conflict rolled the reference drop back along with the delete");
    }


    /// <summary>An owner with one child, and a reference pointing at it.</summary>
    private static async Task<int> Seed(ArticleContext db)
    {
        await db.Database.EnsureCreatedAsync();
        // Stands in for a rowversion: the store moves the token on every UPDATE the application makes.
        await db.Database.ExecuteSqlRawAsync(
            "CREATE TRIGGER IF NOT EXISTS Articles_RowVersion AFTER UPDATE OF Title, CoverImageId, Version ON Articles " +
            "BEGIN UPDATE Articles SET RowVersion = OLD.RowVersion + 1 WHERE Id = NEW.Id; END;");
        var article = new Article { Title = "Release notes" };
        db.Articles.Add(article);
        await db.SaveChangesAsync();
        var image = new ArticleImage { ArticleId = article.Id, FileName = "cover.png" };
        db.ArticleImages.Add(image);
        await db.SaveChangesAsync();
        article.CoverImageId = image.Id;
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return article.Id;
    }
}
