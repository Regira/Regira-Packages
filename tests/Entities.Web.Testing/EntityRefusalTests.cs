using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Validators;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Validators.Abstractions;
using Regira.Entities.Web.Attachments.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.Endpoints;
using Regira.IO.Storage.FileSystem;
using Regira.Utilities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Entities.Web.Testing;

public class GuardedOrder : IEntity<int>, IHasAttachments<GuardedOrderAttachment>
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public ICollection<GuardedOrderAttachment>? Attachments { get; set; }
    public bool? HasAttachment { get; set; }
}

public class GuardedOrderAttachment : EntityAttachment
{
    public GuardedOrderAttachment()
    {
        ObjectType = nameof(GuardedOrder);
    }
}

public class GuardedOrderContext(DbContextOptions<GuardedOrderContext> options) : DbContext(options)
{
    public DbSet<GuardedOrder> Orders => Set<GuardedOrder>();
    public DbSet<GuardedOrderAttachment> OrderAttachments => Set<GuardedOrderAttachment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<GuardedOrder>()
            .HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(x => x.ObjectId);
    }
}

/// <summary>Top-level and public because MVC only discovers controllers that are.</summary>
[Route("guarded-orders")]
public class GuardedOrdersController : EntityControllerBase<GuardedOrder>;

[Route("guarded-orders")]
public class GuardedOrderAttachmentsController : EntityAttachmentControllerBase<GuardedOrderAttachment>;

/// <summary>
/// A rule about who may change a row, thrown from a validator as <see cref="EntityForbiddenException"/> or
/// <see cref="EntityNotFoundException"/>, answers 403 or 404 on every write of both surfaces, attachment uploads included,
/// on a host that registers no exception filter.
/// </summary>
public class EntityRefusalTests
{
    private sealed class JsonMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => JsonSerializer.Deserialize<TTarget>(JsonSerializer.Serialize(source))!;
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
    }

    // "shared" is a row the caller reads but may not change; "parent-gone" refers to a row the caller cannot see
    private static void Validate(IEntityValidatorContext<GuardedOrder> ctx)
    {
        switch (ctx.Item.Code)
        {
            case "shared":
                throw new EntityForbiddenException("Only the owner may change this order.");
            case "parent-gone":
                throw new EntityNotFoundException();
        }
    }

    private static async Task ValidateAttachment(IEntityValidatorContext<GuardedOrderAttachment> ctx, GuardedOrderContext db, CancellationToken token)
    {
        if (await db.Orders.AnyAsync(x => x.Id == ctx.Item.ObjectId && x.Code == "shared", token))
        {
            throw new EntityForbiddenException("Only the owner may add files.");
        }
    }

    private sealed class Host(WebApplication app, SqliteConnection connection, string filesFolder, int sharedId, int goneId) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();
        public int SharedId { get; } = sharedId;
        public int GoneId { get; } = goneId;

        public static async Task<Host> CreateAsync(bool controllers)
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            var filesFolder = Path.Combine(Path.GetTempPath(), $"regira-guarded-orders-{Guid.NewGuid():n}");

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            if (controllers)
            {
                builder.Services.AddControllers().AddApplicationPart(typeof(GuardedOrdersController).Assembly);
            }
            builder.Services.AddDbContext<GuardedOrderContext>(db => db.UseSqlite(connection));
            builder.Services.AddSingleton<IEntityMapper, JsonMapper>();
            builder.Services
                .UseEntities<GuardedOrderContext>(o =>
                {
                    o.UseDefaults();
                    o.AddValidator<GuardedOrderContext, GuardedOrderAttachment>(ValidateAttachment);
                })
                .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = filesFolder }))
                .For<GuardedOrder>(e =>
                {
                    e.Validate(Validate);
                    e.HasAttachments(x => x.Attachments);
                    if (!controllers)
                    {
                        e.Endpoints(o => o.UseDtos<GuardedOrder, GuardedOrder>());
                    }
                });

            var app = builder.Build();
            if (controllers)
            {
                app.MapControllers();
            }
            else
            {
                app.MapEntityEndpoints();
            }

            int sharedId, goneId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<GuardedOrderContext>();
                await db.Database.EnsureCreatedAsync();
                var shared = new GuardedOrder { Code = "shared" };
                var gone = new GuardedOrder { Code = "parent-gone" };
                db.Orders.AddRange(shared, gone);
                await db.SaveChangesAsync();
                sharedId = shared.Id;
                goneId = gone.Id;
            }

            await app.StartAsync();
            return new Host(app, connection, filesFolder, sharedId, goneId);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            connection.Close();
            if (Directory.Exists(filesFolder))
            {
                Directory.Delete(filesFolder, true);
            }
        }
    }

    private static async Task AssertProblem(HttpResponseMessage response, HttpStatusCode status, string detail)
    {
        Assert.Equal(status, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal((int)status, problem!.Status);
        Assert.Equal(detail, problem.Detail);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Forbidden_Write_Answers_403_On_Every_Write(bool controllers)
    {
        await using var host = await Host.CreateAsync(controllers);
        const string detail = "Only the owner may change this order.";

        await AssertProblem(await host.Client.PostAsJsonAsync("/guarded-orders", new GuardedOrder { Code = "shared" }), HttpStatusCode.Forbidden, detail);
        await AssertProblem(await host.Client.PutAsJsonAsync($"/guarded-orders/{host.SharedId}", new GuardedOrder { Id = host.SharedId, Code = "shared" }), HttpStatusCode.Forbidden, detail);
        await AssertProblem(await host.Client.PatchAsync($"/guarded-orders/{host.SharedId}", JsonContent.Create(new { })), HttpStatusCode.Forbidden, detail);
        await AssertProblem(await host.Client.DeleteAsync($"/guarded-orders/{host.SharedId}"), HttpStatusCode.Forbidden, detail);
        var upload = await host.Client.PostAsync($"/guarded-orders/{host.SharedId}/files",
            new MultipartFormDataContent { { new ByteArrayContent("Terms"u8.ToArray()), "file", "terms.txt" } });
        await AssertProblem(upload, HttpStatusCode.Forbidden, "Only the owner may add files.");
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Missing_Row_Answers_404_With_The_Default_Detail_On_Every_Write(bool controllers)
    {
        await using var host = await Host.CreateAsync(controllers);

        const string detail = EntityNotFoundException.ClientMessage;

        await AssertProblem(await host.Client.PostAsJsonAsync("/guarded-orders", new GuardedOrder { Code = "parent-gone" }), HttpStatusCode.NotFound, detail);
        await AssertProblem(await host.Client.PutAsJsonAsync($"/guarded-orders/{host.GoneId}", new GuardedOrder { Id = host.GoneId, Code = "parent-gone" }), HttpStatusCode.NotFound, detail);
        await AssertProblem(await host.Client.PatchAsync($"/guarded-orders/{host.GoneId}", JsonContent.Create(new { })), HttpStatusCode.NotFound, detail);
        await AssertProblem(await host.Client.DeleteAsync($"/guarded-orders/{host.GoneId}"), HttpStatusCode.NotFound, detail);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.PostAsJsonAsync("/guarded-orders", new GuardedOrder { Code = "mine" })).StatusCode);
    }
}
