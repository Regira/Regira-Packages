using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Attributes;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// A <c>[ServerOwned]</c> property on an attachment link, saved through its owner: the owner's sync restores it on a kept
/// link, as the link's own route does, and leaves it to the owner's prepper on a link that brings a new file.
/// </summary>
[TestFixture]
public class AttachmentLinkServerOwnedTests
{
    public class Ticket : IEntity<int>, IHasAttachments, IHasAttachments<TicketPhoto>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<TicketPhoto>? Attachments { get; set; }
        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<TicketPhoto>().ToList();
        }
    }

    public class TicketPhoto : EntityAttachment
    {
        public TicketPhoto() => ObjectType = nameof(Ticket);
        [ServerOwned] public byte[]? Thumbnail { get; set; }
    }

    public class DeskContext(DbContextOptions<DeskContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketPhoto> TicketPhotos => Set<TicketPhoto>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<TicketPhoto>().HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
            modelBuilder.Entity<Ticket>().HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id);
        }
    }

    private static readonly byte[] StoredThumbnail = "stored"u8.ToArray();

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;
    private int _ticketId, _linkId, _attachmentId;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-link-server-owned-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<DeskContext>(db => db.UseSqlite(_connection));
        services.UseEntities<DeskContext>(o => o.UseDefaults())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Ticket>(e =>
            {
                // the owner's prepper derives the thumbnail from new bytes, before the sync
                e.Prepare(ticket =>
                {
                    foreach (var photo in ticket.Attachments?.Where(x => x.NewBytes?.Length > 0) ?? [])
                    {
                        photo.Thumbnail = photo.NewBytes!.Take(3).ToArray();
                    }
                });
                e.HasAttachments(x => x.Attachments);
            });
        _sp = services.BuildServiceProvider();

        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<DeskContext>().Database.EnsureCreatedAsync();
        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
        var ticket = new Ticket { Subject = "Printer" };
        await tickets.Add(ticket);
        await tickets.SaveChanges();
        var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketPhoto, int>>();
        var link = new TicketPhoto
        {
            ObjectId = ticket.Id,
            Thumbnail = StoredThumbnail,
            Attachment = new Attachment { FileName = "photo.jpg", Bytes = "photo"u8.ToArray() }
        };
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

    [Test]
    public async Task A_Kept_Link_Keeps_Its_Stored_Value()
    {
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, Thumbnail = "forged"u8.ToArray() });

        Assert.That(await StoredThumbnailOfLink(), Is.EqualTo(StoredThumbnail));
    }

    [Test]
    public async Task A_Kept_Link_Sent_Without_The_Value_Keeps_It()
    {
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId });

        Assert.That(await StoredThumbnailOfLink(), Is.EqualTo(StoredThumbnail));
    }

    [Test]
    public async Task A_Link_With_A_New_File_Takes_The_Value_The_Owners_Prepper_Set()
    {
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, NewFileName = "new.jpg", NewBytes = "newer"u8.ToArray() });

        Assert.That(await StoredThumbnailOfLink(), Is.EqualTo("new"u8.ToArray()));
    }

    private async Task Save(TicketPhoto photo)
    {
        using var scope = _sp.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
        await tickets.Modify(new Ticket { Id = _ticketId, Subject = "Printer", Attachments = [photo] });
        await tickets.SaveChanges();
    }

    private async Task<byte[]?> StoredThumbnailOfLink()
    {
        using var scope = _sp.CreateScope();
        return (await scope.ServiceProvider.GetRequiredService<DeskContext>().TicketPhotos.AsNoTracking().SingleAsync(x => x.Id == _linkId)).Thumbnail;
    }
}
