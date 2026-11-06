using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Attributes;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// A <c>[ServerOwned]</c> property on an attachment link, saved through its owner: the owner's sync restores it on every
/// kept link, as the link's own route does, a link that brings a new file included. A value derived from the new file is
/// set by a prepper registered after the sync. The client's concurrency token is compared before the restore. A link
/// sent as new is a create, which the restore does not reach: the owner's prepper sets or clears its values.
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

    public class TicketPhoto : EntityAttachment, IHasConcurrencyToken
    {
        public TicketPhoto() => ObjectType = nameof(Ticket);
        [ServerOwned] public byte[]? Thumbnail { get; set; }
        [ServerOwned] public int? ReviewedById { get; set; }
        [ServerOwned] public Guid ConcurrencyToken { get; set; }
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
    private const int StoredReviewer = 7;

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
        services.UseEntities<DeskContext>(o =>
            {
                o.UseDefaults();
                // the link's prepper, registered after the restore UseDefaults() registers
                o.Services.AddPrepper<TicketPhoto>(photo =>
                {
                    if (photo.NewBytes?.Length > 0) photo.Thumbnail = photo.NewBytes.Take(3).ToArray();
                });
            })
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Ticket>(e =>
            {
                e.HasAttachments(x => x.Attachments);
                // the owner's prepper derives the thumbnail from a new file, after the sync restored the stored one
                e.Prepare(ticket =>
                {
                    foreach (var photo in ticket.Attachments ?? [])
                    {
                        var file = photo.NewBytes?.Length > 0 ? photo.NewBytes : photo.Attachment is { Id: 0 } attachment ? attachment.Bytes : null;
                        if (file?.Length > 0) photo.Thumbnail = file.Take(3).ToArray();
                        else if (photo.Id == 0) photo.Thumbnail = null;
                        // a new link is a create: the guard leaves it alone, so the owner's prepper clears what it does not set
                        if (photo.Id == 0) photo.ReviewedById = null;
                    }
                });
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
            ReviewedById = StoredReviewer,
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
    public async Task A_Kept_Link_With_Only_A_New_Name_Keeps_Its_Stored_Value()
    {
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, NewFileName = "renamed.jpg", Thumbnail = "forged"u8.ToArray() });

        Assert.That(await StoredThumbnailOfLink(), Is.EqualTo(StoredThumbnail));
    }

    [Test]
    public async Task A_Kept_Link_As_The_Template_Sends_It_Keeps_Its_Stored_Value()
    {
        // the front-end template fills NewFileName with the stored name on every kept link
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, NewFileName = "photo.jpg" });

        Assert.That(await StoredThumbnailOfLink(), Is.EqualTo(StoredThumbnail));
    }

    [Test]
    public async Task A_Stale_ServerOwned_Token_On_A_Kept_Link_Is_Refused()
    {
        Guid read;
        using (var scope = _sp.CreateScope())
        {
            read = (await scope.ServiceProvider.GetRequiredService<DeskContext>().TicketPhotos.AsNoTracking().SingleAsync(x => x.Id == _linkId)).ConcurrencyToken;
            // another writer saves the link: the primer mints a new token
            var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketPhoto, int>>();
            await links.Modify(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, ConcurrencyToken = read });
            await links.SaveChanges();
        }

        Assert.That(() => Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, ConcurrencyToken = read }),
            Throws.InstanceOf<EntityConcurrencyException>());
    }

    [Test]
    public async Task A_Link_With_A_New_File_Takes_The_Value_A_Prepper_After_The_Sync_Set()
    {
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, NewFileName = "new.jpg", NewBytes = "newer"u8.ToArray(), Thumbnail = "forged"u8.ToArray() });

        Assert.That(await StoredThumbnailOfLink(), Is.EqualTo("new"u8.ToArray()));
    }

    [Test]
    public async Task A_Link_With_New_Bytes_Ignores_The_Clients_Value()
    {
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, NewFileName = "new.jpg", NewBytes = "newer"u8.ToArray(), ReviewedById = 42 });

        Assert.That((await StoredLink()).ReviewedById, Is.EqualTo(StoredReviewer));
    }

    [Test]
    public async Task A_Link_With_A_New_Attachment_Ignores_The_Clients_Value()
    {
        await Save(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, Attachment = new Attachment { FileName = "new.jpg", Bytes = "newer"u8.ToArray() }, ReviewedById = 42, Thumbnail = "forged"u8.ToArray() });

        var stored = await StoredLink();
        Assert.That(stored.ReviewedById, Is.EqualTo(StoredReviewer));
        // derived from the new attachment's bytes by the prepper after the sync
        Assert.That(stored.Thumbnail, Is.EqualTo("new"u8.ToArray()));
    }

    [Test]
    public async Task On_The_Links_Own_Route_A_New_File_Takes_The_Value_A_Prepper_After_UseDefaults_Set()
    {
        using (var scope = _sp.CreateScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketPhoto, int>>();
            await links.Modify(new TicketPhoto { Id = _linkId, ObjectId = _ticketId, AttachmentId = _attachmentId, NewFileName = "new.jpg", NewBytes = "newer"u8.ToArray(), Thumbnail = "forged"u8.ToArray(), ReviewedById = 42 });
            await links.SaveChanges();
        }

        var stored = await StoredLink();
        Assert.That(stored.Thumbnail, Is.EqualTo("new"u8.ToArray()));
        Assert.That(stored.ReviewedById, Is.EqualTo(StoredReviewer));
    }

    [Test]
    public async Task A_Link_Sent_Again_As_New_Takes_What_The_Owners_Prepper_Sets()
    {
        // the kept link dropped and sent without its id: a create on the same file, which the restore does not reach
        await Save(new TicketPhoto { ObjectId = _ticketId, AttachmentId = _attachmentId, ReviewedById = 42, Thumbnail = "forged"u8.ToArray() });

        using var scope = _sp.CreateScope();
        var links = await scope.ServiceProvider.GetRequiredService<DeskContext>().TicketPhotos.AsNoTracking().Where(x => x.ObjectId == _ticketId).ToListAsync();
        Assert.That(links, Has.Count.EqualTo(1));
        Assert.Multiple(() =>
        {
            Assert.That(links[0].Id, Is.Not.EqualTo(_linkId));
            Assert.That(links[0].AttachmentId, Is.EqualTo(_attachmentId));
            Assert.That(links[0].ReviewedById, Is.Null);
            Assert.That(links[0].Thumbnail, Is.Null);
        });
    }

    private async Task Save(TicketPhoto photo)
    {
        using var scope = _sp.CreateScope();
        var tickets = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
        await tickets.Modify(new Ticket { Id = _ticketId, Subject = "Printer", Attachments = [photo] });
        await tickets.SaveChanges();
    }

    private async Task<byte[]?> StoredThumbnailOfLink() => (await StoredLink()).Thumbnail;

    private async Task<TicketPhoto> StoredLink()
    {
        using var scope = _sp.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<DeskContext>().TicketPhotos.AsNoTracking().SingleAsync(x => x.Id == _linkId);
    }
}
