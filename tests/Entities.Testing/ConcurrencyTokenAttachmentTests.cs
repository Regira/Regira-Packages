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
/// An attachment type carrying <see cref="IHasConcurrencyToken"/>: the owner's save deletes the attachment a removed
/// link leaves from a stub, as for any other type, and the token primer points that stub at the stored token.
/// </summary>
[TestFixture]
public class ConcurrencyTokenAttachmentTests
{
    public class TokenAttachment : Attachment<int>, IHasConcurrencyToken
    {
        public Guid ConcurrencyToken { get; set; }
    }

    public class FolderAttachment : EntityAttachment<int, int, int, TokenAttachment>
    {
        public FolderAttachment() => ObjectType = nameof(Folder);
    }

    public class Folder : IEntity<int>, IHasAttachments<FolderAttachment, int, int, int, TokenAttachment>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<FolderAttachment>? Attachments { get; set; }
    }

    public class FolderContext(DbContextOptions<FolderContext> options) : DbContext(options)
    {
        public DbSet<Folder> Folders => Set<Folder>();
        public DbSet<FolderAttachment> FolderAttachments => Set<FolderAttachment>();
        public DbSet<TokenAttachment> Attachments => Set<TokenAttachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<FolderAttachment>().HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
            modelBuilder.Entity<Folder>().HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id);
        }
    }

    private SqliteConnection _connection = null!;
    private ServiceProvider _sp = null!;
    private string _root = null!;

    [SetUp]
    public async Task Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
        _root = Path.Combine(Path.GetTempPath(), $"regira-token-attachment-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<FolderContext>(db => db.UseSqlite(_connection));
        services.UseEntities<FolderContext>(o => o.UseDefaults())
            .WithAttachments<TokenAttachment, int, AttachmentSearchObject<int>>(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Folder>(e =>
            {
                // the links only: the attachment a removed link leaves is not loaded
                e.Includes((query, _) => query.Include(x => x.Attachments));
                e.HasAttachments<FolderContext, Folder, int, FolderAttachment, int, EntityAttachmentSearchObject<int, int>, int, TokenAttachment>();
            });
        _sp = services.BuildServiceProvider();
        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<FolderContext>().Database.EnsureCreatedAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
        if (Directory.Exists(_root))
        {
            Directory.Delete(_root, recursive: true);
        }
    }

    [Test]
    public async Task Removing_A_Link_Of_A_Concurrency_Token_Attachment_Deletes_Its_Attachment()
    {
        int folderId;
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Folder, int>>();
            var folder = new Folder { Title = "Docs", Attachments = [new FolderAttachment { NewFileName = "a.txt", NewBytes = "a"u8.ToArray() }] };
            await service.Save(folder);
            await service.SaveChanges();
            folderId = folder.Id;
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FolderContext>();
            Assert.That(await db.Attachments.Select(x => x.ConcurrencyToken).SingleAsync(), Is.Not.EqualTo(Guid.Empty), "the primer minted the attachment's token");
        }

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Folder, int>>();
            await service.Save(new Folder { Id = folderId, Title = "Docs", Attachments = [] });
            await service.SaveChanges();
        }

        using (var scope = _sp.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<FolderContext>();
            var links = await db.FolderAttachments.CountAsync();
            var attachments = await db.Attachments.CountAsync();
            Assert.Multiple(() =>
            {
                Assert.That(links, Is.Zero);
                Assert.That(attachments, Is.Zero, "the attachment the link left is deleted");
            });
        }
    }
}
