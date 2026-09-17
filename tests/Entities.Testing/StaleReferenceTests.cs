using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Entities.Testing;

/// <summary>
/// A domain action reads an entity with <c>Details(id)</c> — every include loaded — changes a foreign key and
/// saves it through <c>Modify</c>. The update attaches the whole graph, and EF's attach fixup copies the key of
/// the still-loaded reference navigation back into the foreign key, so without the stale-reference guard the
/// old value is written with no error at all. These pin that a foreign key set to a new key wins, that a
/// navigation the caller re-pointed still wins, that an absent key beside a navigation keeps the relation (a body
/// that sends only the nested object), and that the guard holds for every <c>Related()</c> child — each of which
/// the first child's attach reaches through the parent before its own turn. A collection a <c>Related()</c> sync
/// reads is never emptied behind its back: a child listed there stays with that parent.
/// </summary>
[TestFixture]
public class StaleReferenceTests
{
    public class Status : IEntity<int>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    public class Person : IEntity<int>
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        // inverse collections: loaded by fixup, they would write the old key back through a principal the graph
        // still reaches (Alice is the assignee AND the notes' author)
        public ICollection<Ticket>? AssignedTickets { get; set; }
        public ICollection<TicketNote>? AuthoredNotes { get; set; }
    }

    public class Ticket : IEntity<int>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        public int StatusId { get; set; }
        public Status? Status { get; set; }
        public int? AssigneeId { get; set; }
        public Person? Assignee { get; set; }
        public ICollection<TicketNote>? Notes { get; set; } = new List<TicketNote>(); // a List throws when modified mid-sync
    }

    public class TicketNote : IEntity<int>
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public Ticket? Ticket { get; set; }
        public int AuthorId { get; set; }
        public Person? Author { get; set; }
        public string? Text { get; set; }
    }

    public class DeskContext(DbContextOptions<DeskContext> options) : DbContext(options)
    {
        public DbSet<Status> Statuses => Set<Status>();
        public DbSet<Person> People => Set<Person>();
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketNote> TicketNotes => Set<TicketNote>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Ticket>()
                .HasMany(x => x.Notes)
                .WithOne(x => x.Ticket)
                .HasForeignKey(x => x.TicketId);
            modelBuilder.Entity<Ticket>()
                .HasOne(x => x.Assignee)
                .WithMany(x => x.AssignedTickets)
                .HasForeignKey(x => x.AssigneeId);
            modelBuilder.Entity<TicketNote>()
                .HasOne(x => x.Author)
                .WithMany(x => x.AuthoredNotes)
                .HasForeignKey(x => x.AuthorId);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private int _open, _closed, _alice, _bob, _ticketId, _otherTicketId, _firstNoteId, _secondNoteId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<DeskContext>(db => db.UseSqlite(_connection));
        services.UseEntities<DeskContext>(o => o.UseDefaults())
            .For<Status>()
            .For<Person>()
            .For<Ticket>(e => e
                .Related(x => x.Notes)
                .Includes((query, _) => query
                    .Include(x => x.Status)
                    .Include(x => x.Assignee)
                    .Include(x => x.Notes!).ThenInclude(n => n.Author)));
        _sp = services.BuildServiceProvider();

        var db = _sp.GetRequiredService<DeskContext>();
        await db.Database.EnsureCreatedAsync();

        var open = new Status { Title = "Open" };
        var closed = new Status { Title = "Closed" };
        var alice = new Person { Name = "Alice" };
        var bob = new Person { Name = "Bob" };
        db.AddRange(open, closed, alice, bob);
        await db.SaveChangesAsync();

        // Alice is the assignee and the author of both notes, so one Details graph shares her instance
        var first = new TicketNote { AuthorId = alice.Id, Text = "Looking into it" };
        var second = new TicketNote { AuthorId = alice.Id, Text = "Ordered a part" };
        var ticket = new Ticket
        {
            Subject = "Printer",
            StatusId = open.Id,
            AssigneeId = alice.Id,
            Notes = [first, second]
        };
        var other = new Ticket { Subject = "Scanner", StatusId = open.Id };
        db.AddRange(ticket, other);
        await db.SaveChangesAsync();

        (_open, _closed, _alice, _bob) = (open.Id, closed.Id, alice.Id, bob.Id);
        (_ticketId, _otherTicketId, _firstNoteId, _secondNoteId) = (ticket.Id, other.Id, first.Id, second.Id);
        db.ChangeTracker.Clear();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
    }

    private async Task<Ticket> Save(Action<Ticket> change)
    {
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
            var item = (await service.Details(_ticketId))!;
            Assert.That(item.Status, Is.Not.Null, "the navigations must be loaded for the test to mean anything");
            Assert.That(item.Assignee!.AssignedTickets, Does.Contain(item), "the old principal's inverse collection must hold the entity");
            change(item);
            await service.Modify(item);
            await service.SaveChanges();
        }

        using var readScope = _sp.CreateScope();
        return (await readScope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>().Details(_ticketId))!;
    }

    private static TicketNote Note(Ticket item, int id) => item.Notes!.Single(n => n.Id == id);

    [Test]
    public async Task A_Foreign_Key_Changed_Under_A_Loaded_Navigation_Is_Saved()
    {
        var persisted = await Save(item => item.StatusId = _closed);

        Assert.That(persisted.StatusId, Is.EqualTo(_closed));
    }

    [Test]
    public async Task A_Foreign_Key_Changed_While_The_Old_Principal_Stays_In_The_Graph_Is_Saved()
    {
        // Alice stays reachable as the notes' author, and her AssignedTickets still lists the ticket
        var persisted = await Save(item => item.AssigneeId = _bob);

        Assert.That(persisted.AssigneeId, Is.EqualTo(_bob));
    }

    [Test]
    public async Task A_Relation_Cleared_With_Its_Navigation_Is_Saved()
    {
        var persisted = await Save(item =>
        {
            item.AssigneeId = null;
            item.Assignee = null;
        });

        Assert.That(persisted.AssigneeId, Is.Null);
    }

    [Test]
    public async Task An_Absent_Foreign_Key_Beside_Its_Navigation_Keeps_The_Relation()
    {
        // what a body carrying only the nested object maps to
        var persisted = await Save(item => item.AssigneeId = null);

        Assert.That(persisted.AssigneeId, Is.EqualTo(_alice));
    }

    [Test]
    public async Task A_Navigation_The_Caller_Repointed_Still_Decides_The_Foreign_Key()
    {
        var persisted = await Save(item => item.Status = new Status { Id = _closed, Title = "Closed" });

        Assert.That(persisted.StatusId, Is.EqualTo(_closed));
    }

    [Test]
    public async Task A_Foreign_Key_And_A_Matching_Navigation_Changed_Together_Are_Saved()
    {
        var persisted = await Save(item =>
        {
            item.AssigneeId = _bob;
            item.Assignee = new Person { Id = _bob, Name = "Bob" };
        });

        Assert.That(persisted.AssigneeId, Is.EqualTo(_bob));
    }

    [Test]
    public async Task The_Guard_Holds_When_A_Related_Childs_Back_Reference_Attaches_The_Entity_First()
    {
        var persisted = await Save(item =>
        {
            Assert.That(Note(item, _firstNoteId).Ticket, Is.SameAs(item), "the child must reference its parent for this path");
            item.StatusId = _closed;
            Note(item, _firstNoteId).Text = "Fixed";
        });

        Assert.Multiple(() =>
        {
            Assert.That(persisted.StatusId, Is.EqualTo(_closed));
            Assert.That(Note(persisted, _firstNoteId).Text, Is.EqualTo("Fixed"));
        });
    }

    [TestCase(0)]
    [TestCase(1)]
    public async Task Any_Related_Childs_Foreign_Key_Changed_Under_Its_Loaded_Navigation_Is_Saved(int index)
    {
        var noteId = index == 0 ? _firstNoteId : _secondNoteId;

        var persisted = await Save(item => Note(item, noteId).AuthorId = _bob);

        Assert.Multiple(() =>
        {
            Assert.That(Note(persisted, noteId).AuthorId, Is.EqualTo(_bob));
            Assert.That(Note(persisted, noteId == _firstNoteId ? _secondNoteId : _firstNoteId).AuthorId, Is.EqualTo(_alice));
            Assert.That(persisted.AssigneeId, Is.EqualTo(_alice), "the shared principal stays untouched on the parent");
            Assert.That(persisted.StatusId, Is.EqualTo(_open));
        });
    }

    [Test]
    public async Task A_Related_Child_Given_Another_Parent_Key_Stays_With_The_Parent_That_Lists_It()
    {
        // the Notes sync reads that collection: taking the note out of it would be a delete, and mid-sync a List throws
        var persisted = await Save(item => Note(item, _firstNoteId).TicketId = _otherTicketId);

        Assert.Multiple(() =>
        {
            Assert.That(persisted.Notes!.Select(n => n.Id), Is.EquivalentTo(new[] { _firstNoteId, _secondNoteId }));
            Assert.That(Note(persisted, _firstNoteId).TicketId, Is.EqualTo(_ticketId));
        });
    }
}

/// <summary>
/// One row listed in two <c>Related()</c> collections of the entity being saved. Changing its other foreign key must
/// not take it out of either collection: each sync would read the gap as a delete of an instance the other already
/// tracks.
/// </summary>
[TestFixture]
public class StaleReferenceTwoCollectionsTests
{
    public class Person : IEntity<int>
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public ICollection<Ticket>? AssignedTickets { get; set; }
        public ICollection<Ticket>? ReportedTickets { get; set; }
    }

    public class Ticket : IEntity<int>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        public int? AssigneeId { get; set; }
        public Person? Assignee { get; set; }
        public int? ReporterId { get; set; }
        public Person? Reporter { get; set; }
    }

    public class DeskContext(DbContextOptions<DeskContext> options) : DbContext(options)
    {
        public DbSet<Person> People => Set<Person>();
        public DbSet<Ticket> Tickets => Set<Ticket>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Ticket>().HasOne(x => x.Assignee).WithMany(x => x.AssignedTickets).HasForeignKey(x => x.AssigneeId);
            modelBuilder.Entity<Ticket>().HasOne(x => x.Reporter).WithMany(x => x.ReportedTickets).HasForeignKey(x => x.ReporterId);
        }
    }

    [Test]
    public async Task Changing_The_Other_Key_Of_A_Row_Both_Collections_List_Does_Not_Throw()
    {
        await using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var services = new ServiceCollection();
        services.AddDbContext<DeskContext>(db => db.UseSqlite(connection));
        services.UseEntities<DeskContext>(o => o.UseDefaults())
            .For<Ticket>()
            .For<Person>(e => e
                .Related(x => x.AssignedTickets)
                .Related(x => x.ReportedTickets)
                .Includes((query, _) => query.Include(x => x.AssignedTickets).Include(x => x.ReportedTickets)));
        await using var sp = services.BuildServiceProvider();

        int aliceId, bobId, ticketId;
        using (var scope = sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<DeskContext>();
            await db.Database.EnsureCreatedAsync();
            var alice = new Person { Name = "Alice" };
            var bob = new Person { Name = "Bob" };
            db.AddRange(alice, bob);
            await db.SaveChangesAsync();
            var ticket = new Ticket { Subject = "Printer", AssigneeId = alice.Id, ReporterId = alice.Id };
            db.Add(ticket);
            await db.SaveChangesAsync();
            (aliceId, bobId, ticketId) = (alice.Id, bob.Id, ticket.Id);
        }

        using (var scope = sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Person, int>>();
            var alice = (await service.Details(aliceId))!;
            Assert.That(alice.ReportedTickets!.Single(), Is.SameAs(alice.AssignedTickets!.Single()), "one instance in both collections");

            alice.ReportedTickets!.Single().ReporterId = bobId;
            Assert.DoesNotThrowAsync(async () =>
            {
                await service.Modify(alice);
                await service.SaveChanges();
            });
        }

        using (var scope = sp.CreateScope())
        {
            var ticket = await scope.ServiceProvider.GetRequiredService<DeskContext>().Tickets.AsNoTracking().SingleAsync(x => x.Id == ticketId);
            Assert.That(ticket.AssigneeId, Is.EqualTo(aliceId), "the row stays in both collections");
        }
    }
}
