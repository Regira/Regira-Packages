using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// An owner whose <c>Includes</c> does not load its attachment links: its save syncs them against the stored links, as
/// it does when the read loads them.
/// </summary>
[TestFixture]
public class AttachmentSyncWithoutIncludesTests
{
    public class Ticket : IEntity<int>, IHasAttachments, IHasAttachments<TicketAttachment>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<TicketAttachment>? Attachments { get; set; }
        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<TicketAttachment>().ToList();
        }
    }

    public class TicketAttachment : EntityAttachment
    {
        public TicketAttachment() => ObjectType = nameof(Ticket);
    }

    public class DeskContext(DbContextOptions<DeskContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TicketAttachment>().HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
            modelBuilder.Entity<Ticket>().HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;
    private int _ticketId, _linkId, _attachmentId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<DeskContext>(db => db.UseSqlite(_connection));
        services.UseEntities<DeskContext>(o => o.UseDefaults())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Ticket>(e => e.HasAttachments(x => x.Attachments));
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DeskContext>().Database.EnsureCreatedAsync();
        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
        var ticket = new Ticket { Subject = "Printer" };
        await tickets.Add(ticket);
        await tickets.SaveChanges();
        var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketAttachment, int>>();
        var link = new TicketAttachment { ObjectId = ticket.Id, Attachment = new Attachment { FileName = "log.txt", Bytes = "log"u8.ToArray() } };
        await links.Add(link);
        await links.SaveChanges();
        (_ticketId, _linkId, _attachmentId) = (ticket.Id, link.Id, link.AttachmentId);
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
        Directory.Delete(_root, true);
    }

    [TestCase(0, false)]
    [TestCase(-5, false)]
    [TestCase(-5, true)]
    public async Task A_New_Link_Saves_With_The_Owner(int id, bool nested)
    {
        var added = nested
            ? new TicketAttachment { Id = id, ObjectId = _ticketId, Attachment = new Attachment { FileName = "photo.txt", Bytes = "photo"u8.ToArray() } }
            : new TicketAttachment { Id = id, ObjectId = _ticketId, NewFileName = "photo.txt", NewBytes = "photo"u8.ToArray() };
        await Save(new Ticket
        {
            Id = _ticketId,
            Subject = "Printer, edited",
            Attachments = [new TicketAttachment { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId }, added]
        });

        var stored = await StoredLinks();
        Assert.That(stored.Select(x => x.Attachment!.FileName), Is.EqualTo(new[] { "log.txt", "photo.txt" }));
    }

    [Test]
    public async Task A_Link_The_Owner_Leaves_Is_Deleted_With_Its_Attachment()
    {
        await Save(new Ticket { Id = _ticketId, Subject = "Printer, edited", Attachments = [] });

        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DeskContext>();
        Assert.Multiple(async () =>
        {
            Assert.That(await StoredLinks(), Is.Empty);
            Assert.That(await db.Attachments.CountAsync(), Is.Zero);
        });
    }

    private async Task Save(Ticket ticket)
    {
        using var scope = _sp.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
        await tickets.Modify(ticket);
        await tickets.SaveChanges();
    }

    private async Task<List<TicketAttachment>> StoredLinks()
    {
        using var scope = _sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DeskContext>().TicketAttachments.AsNoTracking()
            .Include(x => x.Attachment).OrderBy(x => x.Id).ToListAsync();
    }
}
