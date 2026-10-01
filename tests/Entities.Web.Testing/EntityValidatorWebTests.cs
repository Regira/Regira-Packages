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
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Validators.Abstractions;
using Regira.Entities.Web.Attachments.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.Models;
using Regira.IO.Storage.FileSystem;
using Regira.Utilities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Entities.Web.Testing;

public class ValidatedOrder : IEntity<int>, IHasAttachments<ValidatedOrderAttachment>
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public bool IsLocked { get; set; }
    public ICollection<ValidatedOrderAttachment>? Attachments { get; set; }
    public bool? HasAttachment { get; set; }
}

public class ValidatedOrderAttachment : EntityAttachment
{
    public ValidatedOrderAttachment()
    {
        ObjectType = nameof(ValidatedOrder);
    }
}

public class ValidatedOrderContext(DbContextOptions<ValidatedOrderContext> options) : DbContext(options)
{
    public DbSet<ValidatedOrder> Orders => Set<ValidatedOrder>();
    public DbSet<ValidatedOrderAttachment> OrderAttachments => Set<ValidatedOrderAttachment>();
    public DbSet<Attachment> Attachments => Set<Attachment>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<ValidatedOrder>()
            .HasMany(x => x.Attachments)
            .WithOne()
            .HasForeignKey(x => x.ObjectId);
    }
}

/// <summary>Top-level and public because MVC only discovers controllers that are (see IsController).</summary>
[Route("validated-orders")]
public class ValidatedOrdersController : EntityControllerBase<ValidatedOrder>;

[Route("validated-orders")]
public class ValidatedOrderAttachmentsController : EntityAttachmentControllerBase<ValidatedOrderAttachment>;

/// <summary>
/// Over HTTP, on a host that registers no exception filter (<c>MapEntityExceptions()</c> or
/// <c>ConfigureDefaultJsonOptions()</c>): every generated write answers a validator's rejection with 400 and a
/// ValidationProblemDetails, a field with several messages carrying each of them.
/// </summary>
public class EntityValidatorWebTests
{
    /// <summary>DTO == entity here, so a JSON round trip is an exact mapper.</summary>
    private sealed class JsonMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => JsonSerializer.Deserialize<TTarget>(JsonSerializer.Serialize(source))!;
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
    }

    private static void Validate(IEntityValidatorContext<ValidatedOrder> ctx)
    {
        var order = ctx.Item;
        if (ctx.Operation == EntityWriteOperation.Remove)
        {
            if (order.IsLocked)
            {
                ctx.AddError(string.Empty, "A locked order cannot be deleted.");
            }
            return;
        }
        if (string.IsNullOrEmpty(order.Code))
        {
            ctx.AddError(nameof(ValidatedOrder.Code), "A code is required.");
        }
        else if (order.Code.Length > 5)
        {
            ctx.AddError(nameof(ValidatedOrder.Code), "TooLong", new { max = 5 });
            ctx.AddError(nameof(ValidatedOrder.Code), "Upper case only.");
        }
    }

    /// <summary>The link entity's rule: an upload, or a file replaced, on a locked order is refused.</summary>
    private static async Task ValidateAttachment(IEntityValidatorContext<ValidatedOrderAttachment> ctx, ValidatedOrderContext db, CancellationToken token)
    {
        if (ctx.Operation != EntityWriteOperation.Remove && await db.Orders.AnyAsync(x => x.Id == ctx.Item.ObjectId && x.IsLocked, token))
        {
            ctx.AddError(string.Empty, "A locked order takes no files.");
        }
    }

    private sealed class Host(WebApplication app, HttpClient client, SqliteConnection connection, string filesFolder,
        int orderId, int lockedId, int lockedAttachmentId) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public int OrderId { get; } = orderId;
        public int LockedId { get; } = lockedId;
        public int LockedAttachmentId { get; } = lockedAttachmentId;

        public static async Task<Host> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();
            var filesFolder = Path.Combine(Path.GetTempPath(), $"regira-validated-orders-{Guid.NewGuid():n}");

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(ValidatedOrdersController).Assembly);
            builder.Services.AddDbContext<ValidatedOrderContext>(db => db.UseSqlite(connection));
            builder.Services.AddSingleton<IEntityMapper, JsonMapper>();
            builder.Services
                .UseEntities<ValidatedOrderContext>(o =>
                {
                    o.UseDefaults();
                    o.AddValidator<ValidatedOrderContext, ValidatedOrderAttachment>(ValidateAttachment);
                })
                .WithAttachments(_ => new BinaryFileService(new FileSystemOptions { RootFolder = filesFolder }))
                .For<ValidatedOrder>(e =>
                {
                    e.Validate(Validate);
                    e.HasAttachments(x => x.Attachments);
                });

            var app = builder.Build();
            app.MapControllers();

            int orderId, lockedId, lockedAttachmentId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ValidatedOrderContext>();
                await db.Database.EnsureCreatedAsync();
                var order = new ValidatedOrder { Code = "A1" };
                var locked = new ValidatedOrder { Code = "B2", IsLocked = true };
                db.Orders.AddRange(order, locked);
                await db.SaveChangesAsync();
                var terms = new ValidatedOrderAttachment
                {
                    ObjectId = locked.Id,
                    Attachment = new Attachment { FileName = "terms.txt", Bytes = "Terms and conditions"u8.ToArray() }
                };
                db.OrderAttachments.Add(terms);
                await db.SaveChangesAsync();
                orderId = order.Id;
                lockedId = locked.Id;
                lockedAttachmentId = terms.Id;
            }

            await app.StartAsync();
            return new Host(app, app.GetTestClient(), connection, filesFolder, orderId, lockedId, lockedAttachmentId);
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

    private static MultipartFormDataContent FileContent(string fileName)
        => new() { { new ByteArrayContent("Terms and conditions"u8.ToArray()), "file", fileName } };

    private static async Task<IDictionary<string, string[]>> ErrorsOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<ValidationProblemDetails>())!.Errors;
    }

    [Fact]
    public async Task The_400_Is_A_Problem_Listing_Every_Error_With_Its_Args()
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.PutAsJsonAsync($"/validated-orders/{host.OrderId}",
            new ValidatedOrder { Id = host.OrderId, Code = "toolong" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal("application/problem+json", response.Content.Headers.ContentType?.MediaType);
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // the app's ProblemDetailsFactory built it, as it builds model binding's 400
        Assert.True(body.RootElement.TryGetProperty("traceId", out _));
        Assert.Equal(
            """[{"key":"Code","message":"TooLong","args":{"max":5}},{"key":"Code","message":"Upper case only."}]""",
            body.RootElement.GetProperty("errorDetails").GetRawText());
    }

    [Fact]
    public async Task Post_Answers_400_With_The_Error_Map()
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.PostAsJsonAsync("/validated-orders", new ValidatedOrder());

        Assert.Equal(["A code is required."], (await ErrorsOf(response))["Code"]);
    }

    [Fact]
    public async Task Put_Answers_400_With_Every_Message_Of_A_Field()
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.PutAsJsonAsync($"/validated-orders/{host.OrderId}",
            new ValidatedOrder { Id = host.OrderId, Code = "toolong" });

        Assert.Equal(["TooLong", "Upper case only."], (await ErrorsOf(response))["Code"]);
    }

    [Fact]
    public async Task Patch_Answers_400_With_The_Error_Map()
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.PatchAsync($"/validated-orders/{host.OrderId}", JsonContent.Create(new { code = "" }));

        Assert.Equal(["A code is required."], (await ErrorsOf(response))["Code"]);
    }

    [Fact]
    public async Task Delete_Answers_400_Without_An_Exception_Filter()
    {
        await using var host = await Host.CreateAsync();

        var rejected = await host.Client.DeleteAsync($"/validated-orders/{host.LockedId}");
        var accepted = await host.Client.DeleteAsync($"/validated-orders/{host.OrderId}");

        Assert.Equal(["A locked order cannot be deleted."], (await ErrorsOf(rejected))[""]);
        Assert.Equal(HttpStatusCode.OK, accepted.StatusCode);
        var list = await host.Client.GetFromJsonAsync<ListResult<ValidatedOrder>>("/validated-orders");
        Assert.Equal([host.LockedId], list!.Items!.Select(x => x.Id));
    }

    [Fact]
    public async Task Upload_And_File_Replace_Answer_400_Without_An_Exception_Filter()
    {
        await using var host = await Host.CreateAsync();

        var upload = await host.Client.PostAsync($"/validated-orders/{host.LockedId}/files", FileContent("terms-v2.txt"));
        var replace = await host.Client.PutAsync($"/validated-orders/{host.LockedId}/files/{host.LockedAttachmentId}", FileContent("terms-v2.txt"));

        Assert.Equal(["A locked order takes no files."], (await ErrorsOf(upload))[""]);
        Assert.Equal(["A locked order takes no files."], (await ErrorsOf(replace))[""]);
    }
}
