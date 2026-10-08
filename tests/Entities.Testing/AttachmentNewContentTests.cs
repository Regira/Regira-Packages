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
}
