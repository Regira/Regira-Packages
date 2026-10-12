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
/// Every way code creates an attachment: a new link carries its file either as a nested <c>Attachment</c> or as
/// <c>NewFileName</c> + <c>NewBytes</c>, saved through the owner — created or updated — or through the link's own
/// service. Each one stores the file and links it.
/// </summary>
[TestFixture]
public class AttachmentNewContentTests
{
    public class Product : IEntity<int>, IHasAttachments, IHasAttachments<ProductAttachment>
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        [NotMapped] public bool? HasAttachment { get; set; }
        public ICollection<ProductAttachment>? Attachments { get; set; }
        ICollection<IEntityAttachment>? IHasAttachments.Attachments
        {
            get => Attachments?.Cast<IEntityAttachment>().ToArray();
            set => Attachments = value?.Cast<ProductAttachment>().ToList();
        }
    }

    public class ProductAttachment : EntityAttachment
    {
        public ProductAttachment() => ObjectType = nameof(Product);
    }

    public class ShopContext(DbContextOptions<ShopContext> options) : DbContext(options)
    {
        public DbSet<Product> Products => Set<Product>();
        public DbSet<ProductAttachment> ProductAttachments => Set<ProductAttachment>();
        public DbSet<Attachment> Attachments => Set<Attachment>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<ProductAttachment>().HasOne(x => x.Attachment).WithMany().HasForeignKey(x => x.AttachmentId);
            modelBuilder.Entity<Product>().HasMany(x => x.Attachments).WithOne().HasForeignKey(x => x.ObjectId).HasPrincipalKey(x => x.Id);
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
        _root = Path.Combine(Path.GetTempPath(), $"regira-newcontent-{Guid.NewGuid():N}");
        Directory.CreateDirectory(_root);

        var services = new ServiceCollection();
        services.AddDbContext<ShopContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ShopContext>(o => o.UseDefaults())
            .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = _root }))
            .For<Product>(e =>
            {
                e.Includes((query, _) => query.IncludeEntityAttachments());
                e.HasAttachments<ShopContext, Product, ProductAttachment>(x => x.Attachments);
            });
        _sp = services.BuildServiceProvider();
        using var scope = _sp.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ShopContext>().Database.EnsureCreatedAsync();
    }

    [TearDown]
    public void TearDown()
    {
        _sp.Dispose();
        _connection.Close();
        Directory.Delete(_root, true);
    }

    private static byte[] Content => "spec sheet"u8.ToArray();
    private static ProductAttachment NewBytesLink(int objectId = 0) => new() { ObjectId = objectId, NewFileName = "spec.txt", NewBytes = Content };
    private static ProductAttachment NestedLink(int objectId = 0) => new()
    {
        ObjectId = objectId,
        Attachment = new Attachment { FileName = "spec.txt", Bytes = Content },
    };

    private async Task<int> AddProduct(params ProductAttachment[] links)
    {
        using var scope = _sp.CreateScope();
        var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();
        var product = new Product { Title = "Desk", Attachments = links.Length > 0 ? links.ToList() : null };
        await service.Add(product);
        await service.SaveChanges();
        return product.Id;
    }

    private void AssertStoredFile(int productId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var link = db.ProductAttachments.Include(x => x.Attachment).AsNoTracking().Single(x => x.ObjectId == productId);
        Assert.Multiple(() =>
        {
            Assert.That(link.AttachmentId, Is.GreaterThan(0));
            Assert.That(link.Attachment!.FileName, Is.EqualTo("spec.txt"));
            Assert.That(link.Attachment.Path, Is.Not.Null.And.Not.Empty, "the file was stored");
            Assert.That(File.ReadAllBytes(Path.Combine(_root, link.Attachment.Path!)), Is.EqualTo(Content));
        });
    }

    [Test]
    public async Task A_Created_Owner_Stores_A_Nested_Attachment()
        => AssertStoredFile(await AddProduct(NestedLink()));

    [Test]
    public async Task A_Created_Owner_Stores_New_Bytes()
        => AssertStoredFile(await AddProduct(NewBytesLink()));

    [Test]
    public async Task An_Updated_Owner_Stores_New_Bytes()
    {
        var productId = await AddProduct();
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();
            var product = (await service.Details(productId))!;
            product.Attachments = [NewBytesLink()];
            await service.Modify(product);
            await service.SaveChanges();
        }
        AssertStoredFile(productId);
    }

    [Test]
    public async Task The_Link_Service_Stores_A_Nested_Attachment()
    {
        var productId = await AddProduct();
        using (var scope = _sp.CreateScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<IEntityService<ProductAttachment, int>>();
            await links.Add(NestedLink(productId));
            await links.SaveChanges();
        }
        AssertStoredFile(productId);
    }

    [Test]
    public async Task The_Link_Service_Stores_New_Bytes()
    {
        var productId = await AddProduct();
        using (var scope = _sp.CreateScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<IEntityService<ProductAttachment, int>>();
            await links.Add(NewBytesLink(productId));
            await links.SaveChanges();
        }
        AssertStoredFile(productId);
    }
    // A link given a new attachment in place of its own, the AttachmentId it was read with kept: through the owner and
    // through the link's own service, the replaced attachment goes with its file
    [TestCase(true)]
    [TestCase(false)]
    public async Task A_Replaced_Attachment_Goes_On_Both_Routes(bool throughOwner)
    {
        var productId = await AddProduct(NestedLink());
        using (var scope = _sp.CreateScope())
        {
            var replacement = new Attachment { FileName = "spec.txt", Bytes = Content };
            if (throughOwner)
            {
                var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();
                var product = (await service.Details(productId))!;
                product.Attachments!.Single().Attachment = replacement;
                await service.Modify(product);
                await service.SaveChanges();
            }
            else
            {
                var links = scope.ServiceProvider.GetRequiredService<IEntityService<ProductAttachment, int>>();
                var link = (await links.Details(StoredLinkIds(productId).Single()))!;
                link.Attachment = replacement;
                await links.Modify(link);
                await links.SaveChanges();
            }
        }

        using var check = _sp.CreateScope();
        var db = check.ServiceProvider.GetRequiredService<ShopContext>();
        var stored = db.Attachments.AsNoTracking().ToArray();
        Assert.Multiple(() =>
        {
            Assert.That(stored, Has.Length.EqualTo(1), "the replaced attachment row");
            Assert.That(db.ProductAttachments.AsNoTracking().Single().AttachmentId, Is.EqualTo(stored[0].Id));
            Assert.That(Directory.GetFiles(_root, "*", SearchOption.AllDirectories), Has.Length.EqualTo(1), "the replaced file");
        });
    }

    // Two links of one owner can share an attachment: a new link may name one its owner links already. Replacing the
    // attachment of one of them, or removing it, keeps the attachment, and its file, that the other still links — on the
    // owner's route and on the link's own
    private async Task<(int ProductId, int SharedId, int KeptLinkId, int OtherLinkId)> ShareOneAttachment()
    {
        var productId = await AddProduct(NestedLink());
        int sharedId;
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();
            var product = (await service.Details(productId))!;
            sharedId = product.Attachments!.Single().AttachmentId;
            product.Attachments = [.. product.Attachments!, new ProductAttachment { ObjectId = productId, AttachmentId = sharedId }];
            await service.Modify(product);
            await service.SaveChanges();
        }
        var linkIds = StoredLinkIds(productId);
        Assert.That(linkIds, Has.Length.EqualTo(2));
        return (productId, sharedId, linkIds.Max(), linkIds.Min());
    }

    private void AssertSharedAttachmentKept(int keptLinkId, int sharedId)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ShopContext>();
        var kept = db.ProductAttachments.Include(x => x.Attachment).AsNoTracking().SingleOrDefault(x => x.Id == keptLinkId);
        Assert.Multiple(() =>
        {
            Assert.That(kept?.AttachmentId, Is.EqualTo(sharedId), "the other link");
            Assert.That(kept?.Attachment?.Path, Is.Not.Null, "the shared attachment");
            Assert.That(kept?.Attachment?.Path is { } path && File.Exists(Path.Combine(_root, path)), Is.True, "the shared file");
        });
    }

    [TestCase(true)]
    [TestCase(false)]
    public async Task The_Owner_Route_Keeps_An_Attachment_Another_Link_Names(bool replace)
    {
        var (productId, sharedId, keptLinkId, otherLinkId) = await ShareOneAttachment();

        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();
            var product = (await service.Details(productId))!;
            if (replace)
            {
                product.Attachments!.Single(x => x.Id == otherLinkId).Attachment = new Attachment { FileName = "spec-2.txt", Bytes = Content };
            }
            else
            {
                // as a client's input maps it: the link alone, no nested attachment
                var keptLink = product.Attachments!.Single(x => x.Id == keptLinkId);
                keptLink.Attachment = null;
                product.Attachments = [keptLink];
            }
            await service.Modify(product);
            await service.SaveChanges();
        }

        AssertSharedAttachmentKept(keptLinkId, sharedId);
    }

    // one save removes a link and adds another naming its attachment: the attachment stays, with the new link
    [Test]
    public async Task A_New_Link_Taking_Over_A_Removed_Links_Attachment_Keeps_It()
    {
        var productId = await AddProduct(NestedLink());
        int sharedId;
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();
            var product = (await service.Details(productId))!;
            sharedId = product.Attachments!.Single().AttachmentId;
            product.Attachments = [new ProductAttachment { ObjectId = productId, AttachmentId = sharedId }];
            await service.Modify(product);
            await service.SaveChanges();
        }

        AssertSharedAttachmentKept(StoredLinkIds(productId).Single(), sharedId);
    }

    // the file replace (PUT {objectId}/files/{id}) and the delete of a link (DELETE {objectId}/attachments/{id})
    [TestCase(true)]
    [TestCase(false)]
    public async Task The_Link_Route_Keeps_An_Attachment_Another_Link_Names(bool replace)
    {
        var (_, sharedId, keptLinkId, otherLinkId) = await ShareOneAttachment();

        using (var scope = _sp.CreateScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<IEntityService<ProductAttachment, int>>();
            var link = (await links.Details(otherLinkId))!;
            if (replace)
            {
                link.Attachment = new Attachment { FileName = "spec-2.txt", Bytes = Content };
                await links.Save(link);
            }
            else
            {
                await links.Remove(link);
            }
            await links.SaveChanges();
        }

        AssertSharedAttachmentKept(keptLinkId, sharedId);
    }

    // a client mints negative temp ids for new rows; a link that kept one was inserted under it (SQLite) or refused
    // as an explicit key (SQL Server)
    private int[] StoredLinkIds(int productId)
    {
        using var scope = _sp.CreateScope();
        return scope.ServiceProvider.GetRequiredService<ShopContext>().ProductAttachments.AsNoTracking()
            .Where(x => x.ObjectId == productId).Select(x => x.Id).ToArray();
    }

    [Test]
    public async Task A_Created_Owner_Gives_Temp_Id_Links_Store_Ids()
    {
        var first = NewBytesLink(); first.Id = -1;
        var second = NestedLink(); second.Id = -2;

        var productId = await AddProduct(first, second);

        Assert.That(StoredLinkIds(productId), Has.Length.EqualTo(2).And.All.GreaterThan(0));
    }

    [Test]
    public async Task An_Updated_Owner_Gives_A_Temp_Id_Link_A_Store_Id()
    {
        var productId = await AddProduct(NewBytesLink());
        using (var scope = _sp.CreateScope())
        {
            var service = scope.ServiceProvider.GetRequiredService<IEntityService<Product, int>>();
            var product = (await service.Details(productId))!;
            var added = NewBytesLink(productId); added.Id = -1;
            product.Attachments = [.. product.Attachments!, added];
            await service.Modify(product);
            await service.SaveChanges();
        }

        Assert.That(StoredLinkIds(productId), Has.Length.EqualTo(2).And.All.GreaterThan(0));
    }

    [Test]
    public async Task The_Link_Service_Gives_A_Temp_Id_Link_A_Store_Id()
    {
        var productId = await AddProduct();
        using (var scope = _sp.CreateScope())
        {
            var links = scope.ServiceProvider.GetRequiredService<IEntityService<ProductAttachment, int>>();
            var link = NewBytesLink(productId); link.Id = -1;
            await links.Add(link);
            await links.SaveChanges();
        }

        Assert.That(StoredLinkIds(productId), Has.Length.EqualTo(1).And.All.GreaterThan(0));
    }
}
