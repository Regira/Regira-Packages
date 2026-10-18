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
/// An archived owner whose <c>Includes</c> loads neither its links nor its children: the write service reads it
/// archived-inclusive, and its save syncs both against the stored rows, as it does for an owner that is not archived.
/// On <c>net10.0</c> the named archived query filter would hide the archived owner from the stored-row read.
/// </summary>
[TestFixture]
public class ArchivedOwnerSyncWithoutIncludesTests
{
    public class Ticket : IEntity<int>, IArchivable, IHasAttachments, IHasAttachments<TicketAttachment>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        public bool IsArchived { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<TicketAttachment>? Attachments { get; set; }
        public ICollection<TicketNote>? Notes { get; set; }
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

    public class TicketNote : IEntity<int>
    {
        public int Id { get; set; }
        public int TicketId { get; set; }
        public string? Text { get; set; }
    }

    public class DeskContext(DbContextOptions<DeskContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
        public DbSet<TicketNote> TicketNotes => Set<TicketNote>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TicketAttachment>().HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
            modelBuilder.Entity<Ticket>().HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id);
            modelBuilder.Entity<Ticket>().HasMany(x => x.Notes).WithOne().HasForeignKey(x => x.TicketId);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;
    private int _ticketId, _linkId, _attachmentId, _noteId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-archived-sync-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<DeskContext>(db => db.UseSqlite(_connection));
        services.UseEntities<DeskContext>(o => o.UseDefaults())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Ticket>(e =>
            {
                e.HasAttachments(x => x.Attachments);
                e.Related(x => x.Notes);
            });
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DeskContext>().Database.EnsureCreatedAsync();
        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
        var ticket = new Ticket { Subject = "Printer", IsArchived = true, Notes = [new TicketNote { Text = "first" }] };
        await tickets.Add(ticket);
        await tickets.SaveChanges();
        var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketAttachment, int>>();
        var link = new TicketAttachment { ObjectId = ticket.Id, Attachment = new Attachment { FileName = "log.txt", Bytes = "log"u8.ToArray() } };
        await links.Add(link);
        await links.SaveChanges();
        (_ticketId, _linkId, _attachmentId, _noteId) = (ticket.Id, link.Id, link.AttachmentId, ticket.Notes.Single().Id);
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
        Directory.Delete(_root, true);
    }

    [Test]
    public async Task A_New_Link_Saves_With_The_Archived_Owner()
    {
        await Save(new Ticket
        {
            Id = _ticketId,
            Subject = "Printer, edited",
            IsArchived = true,
            Attachments =
            [
                new TicketAttachment { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId },
                new TicketAttachment { ObjectId = _ticketId, NewFileName = "photo.txt", NewBytes = "photo"u8.ToArray() }
            ]
        });

        using var scope = _sp.CreateScope();
        var stored = await scope.ServiceProvider.GetRequiredService<DeskContext>().TicketAttachments.AsNoTracking()
            .Include(x => x.Attachment).OrderBy(x => x.Id).ToListAsync();
        Assert.That(stored.Select(x => x.Attachment!.FileName), Is.EqualTo(new[] { "log.txt", "photo.txt" }));
    }

    [Test]
    public async Task An_Edited_Child_Saves_With_The_Archived_Owner()
    {
        await Save(new Ticket
        {
            Id = _ticketId,
            Subject = "Printer, edited",
            IsArchived = true,
            Notes = [new TicketNote { Id = _noteId, TicketId = _ticketId, Text = "edited" }]
        });

        using var scope = _sp.CreateScope();
        var notes = await scope.ServiceProvider.GetRequiredService<DeskContext>().TicketNotes.AsNoTracking().ToListAsync();
        Assert.That(notes.Select(x => (x.Id, x.Text)), Is.EqualTo(new[] { (_noteId, "edited") }));
    }

    private async Task Save(Ticket ticket)
    {
        using var scope = _sp.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
        await tickets.Modify(ticket);
        await tickets.SaveChanges();
    }
}
