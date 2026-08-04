using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Controllers;
using Regira.Entities.Web.Models;
using Regira.Utilities;

namespace Entities.Web.Testing;

/// <summary>
/// The archived write contract at the controller boundary: <c>DELETE</c> soft-deletes and reports a real
/// affected count, a repeated delete is idempotent, <c>GET /{id}</c> keeps 404-ing on an archived row while
/// <c>?archived=included</c> resolves it, and a write through a <c>TInputDto</c> that cannot express
/// <c>IsArchived</c> never clobbers the persisted flag — while one that can still restores.
/// </summary>
public class ArchivedWriteContractTests : IDisposable
{
    public class Doc : IEntity<int>, IArchivable
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
    }

    public class DocContext(DbContextOptions<DocContext> options) : DbContext(options)
    {
        public DbSet<Doc> Docs => Set<Doc>();

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.SetArchivedQueryFilter();
        }
    }

    public class DocDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
    }

    /// <summary>Input model without <c>IsArchived</c> — the persisted flag must survive a write through it.</summary>
    public class DocInputDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
    }

    /// <summary>Input model that owns <c>IsArchived</c> — it stays the write boundary, so <c>false</c> restores.</summary>
    public class ArchivableDocInputDto
    {
        public int Id { get; set; }
        public string? Title { get; set; }
        public bool IsArchived { get; set; }
    }

    private sealed class ReflectionMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => ObjectUtility.Fill(Activator.CreateInstance<TTarget>(), source);
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
    }

    private sealed class TestController : ControllerBase;

    private readonly SqliteConnection _connection;
    private readonly ServiceProvider _sp;
    private readonly TestController _ctrl;
    private readonly int _id;

    public ArchivedWriteContractTests()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddDbContext<DocContext>(db => db.UseSqlite(_connection));
        services.UseEntities<DocContext>(o => o.UseDefaults()).For<Doc>();
        services.AddSingleton<IEntityMapper, ReflectionMapper>();
        _sp = services.BuildServiceProvider();

        var db = _sp.GetRequiredService<DocContext>();
        db.Database.EnsureCreated();
        var doc = new Doc { Title = "live" };
        db.Docs.Add(doc);
        db.SaveChanges();
        db.ChangeTracker.Clear();
        _id = doc.Id;

        _ctrl = new TestController
        {
            ControllerContext = new ControllerContext
            {
                HttpContext = new DefaultHttpContext { RequestServices = _sp }
            }
        };
    }

    public void Dispose()
    {
        _sp.Dispose();
        _connection.Close();
        GC.SuppressFinalize(this);
    }

    private IEntityService<Doc, int> Service => _sp.GetRequiredService<IEntityService<Doc, int>>();

    private static TResult Payload<TResult>(ActionResult<TResult>? result)
    {
        Assert.NotNull(result);
        var ok = Assert.IsType<OkObjectResult>(result!.Result);
        return Assert.IsType<TResult>(ok.Value);
    }

    [Fact]
    public async Task Delete_Archives_The_Row_And_Reports_The_Real_Affected_Count()
    {
        var payload = Payload(await _ctrl.Delete<Doc, int, DocDto>(_id));

        var persisted = await Service.Details(_id, ArchivedFilter.Included);
        Assert.Equal(1, payload.Affected);
        Assert.NotNull(persisted);
        Assert.True(persisted!.IsArchived);
    }

    [Fact]
    public async Task A_Second_Delete_Is_Idempotent()
    {
        await _ctrl.Delete<Doc, int, DocDto>(_id);

        var second = await _ctrl.Delete<Doc, int, DocDto>(_id);

        Assert.NotNull(second); // null is what the controller turns into a 404
        var persisted = await Service.Details(_id, ArchivedFilter.Included);
        Assert.True(persisted!.IsArchived);
    }

    [Fact]
    public async Task Delete_On_A_Missing_Row_Is_A_404()
        => Assert.Null(await _ctrl.Delete<Doc, int, DocDto>(_id + 1000));

    [Fact]
    public async Task Details_Keeps_404ing_On_An_Archived_Row()
    {
        await _ctrl.Delete<Doc, int, DocDto>(_id);

        Assert.Null(await _ctrl.Details<Doc, int, DocDto>(_id));
    }

    [Fact]
    public async Task Details_With_Archived_Included_Resolves_An_Archived_Row()
    {
        await _ctrl.Delete<Doc, int, DocDto>(_id);

        var payload = Payload(await _ctrl.Details<Doc, int, DocDto>(_id, ArchivedFilter.Included));

        Assert.Equal("live", payload.Item.Title);
        Assert.True(payload.Item.IsArchived);
    }

    [Fact]
    public async Task A_Write_Through_An_Input_Dto_Without_IsArchived_Leaves_The_Row_Archived()
    {
        await _ctrl.Delete<Doc, int, DocDto>(_id);

        var payload = Payload(await _ctrl.Save<Doc, int, DocDto, DocInputDto>(new DocInputDto { Title = "renamed" }, _id));

        var persisted = await Service.Details(_id, ArchivedFilter.Included);
        Assert.Equal(1, payload.Affected);
        Assert.True(payload.Item.IsArchived);
        Assert.Equal("renamed", persisted!.Title);
        Assert.True(persisted.IsArchived);
    }

    [Fact]
    public async Task A_Write_Through_An_Input_Dto_With_IsArchived_False_Restores_The_Row()
    {
        await _ctrl.Delete<Doc, int, DocDto>(_id);

        var payload = Payload(await _ctrl.Save<Doc, int, DocDto, ArchivableDocInputDto>(
            new ArchivableDocInputDto { Title = "restored", IsArchived = false }, _id));

        // reachable through the plain read path again — that is what makes restore work
        var persisted = await Service.Details(_id);
        Assert.False(payload.Item.IsArchived);
        Assert.NotNull(persisted);
        Assert.Equal("restored", persisted!.Title);
        Assert.False(persisted.IsArchived);
    }

    [Fact]
    public async Task A_Write_On_A_Live_Row_Is_Unaffected()
    {
        var payload = Payload(await _ctrl.Save<Doc, int, DocDto, DocInputDto>(new DocInputDto { Title = "renamed" }, _id));

        var persisted = await Service.Details(_id);
        Assert.False(payload.Item.IsArchived);
        Assert.Equal("renamed", persisted!.Title);
        Assert.False(persisted.IsArchived);
    }

    [Fact]
    public async Task A_Write_On_A_Missing_Row_Is_Still_A_404()
        => Assert.Null(await _ctrl.Save<Doc, int, DocDto, DocInputDto>(new DocInputDto { Title = "nope" }, _id + 1000));
}
