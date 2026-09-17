using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.FileSystem;

namespace Entities.Testing;

/// <summary>
/// Attachment links with a back-reference to their owner (the model the archived-owner pattern uses) in a
/// <c>List</c>: the attachment sync walks that list while it tracks each link, so the stale-reference guard must not
/// take a link out of it. A link given another owner id stays with the owner that lists it.
/// </summary>
[TestFixture]
public class StaleReferenceAttachmentTests
{
    public class Ticket : IEntity<int>, IHasAttachments, IHasAttachments<TicketAttachment>
    {
        public int Id { get; set; }
        public string? Subject { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<TicketAttachment>? Attachments { get; set; } = new List<TicketAttachment>();
        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<TicketAttachment>().ToList();
        }
    }

    public class TicketAttachment : EntityAttachment
    {
        public TicketAttachment() => ObjectType = nameof(Ticket);
        public Ticket? Ticket { get; set; }
    }

    public class DeskContext(DbContextOptions<DeskContext> options) : DbContext(options)
    {
        public DbSet<Ticket> Tickets => Set<Ticket>();
        public DbSet<TicketAttachment> TicketAttachments => Set<TicketAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<Ticket>()
                .HasMany(x => x.Attachments)
                .WithOne(a => a.Ticket!)
                .HasForeignKey(x => x.ObjectId)
                .HasPrincipalKey(x => x.Id);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;

    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-stale-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<DeskContext>(db => db.UseSqlite(_connection));
        services.UseEntities<DeskContext>(o => o.UseDefaults())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Ticket>(e =>
            {
                e.Includes((query, _) => query.IncludeEntityAttachments());
                e.HasAttachments(x => x.Attachments);
            });
        _sp = services.BuildServiceProvider();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
        Directory.Delete(_root, true);
    }

    [Test]
    public async Task A_Link_Given_Another_Owner_Id_Stays_With_The_Owner_That_Lists_It()
    {
        int firstId, otherId;
        using (var scope = _sp.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<DeskContext>().Database.EnsureCreatedAsync();
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
            var first = new Ticket { Subject = "Printer" };
            var other = new Ticket { Subject = "Scanner" };
            await service.Add(first);
            await service.Add(other);
            await service.SaveChanges();
            (firstId, otherId) = (first.Id, other.Id);

            var links = scope.ServiceProvider.GetRequiredService<IEntityService<TicketAttachment, int>>();
            foreach (var name in new[] { "log.txt", "photo.txt" })
            {
                await links.Add(new TicketAttachment
                {
                    ObjectId = firstId,
                    Attachment = new Attachment { FileName = name, ContentType = "text/plain", Bytes = "content"u8.ToArray() }
                });
            }
            await links.SaveChanges();
        }

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>();
            var item = (await service.Details(firstId))!;
            Assert.That(item.Attachments, Has.Count.EqualTo(2));
            Assert.That(item.Attachments!.First().Ticket, Is.SameAs(item), "the link must reference its owner for this path");

            item.Attachments!.First().ObjectId = otherId;
            Assert.DoesNotThrowAsync(async () =>
            {
                await service.Modify(item);
                await service.SaveChanges();
            });
        }

        using (var readScope = _sp.CreateScope())
        {
            var persisted = (await readScope.ServiceProvider.GetRequiredService<IEntityService<Ticket, int>>().Details(firstId))!;
            Assert.That(persisted.Attachments, Has.Count.EqualTo(2));
        }
    }
}
