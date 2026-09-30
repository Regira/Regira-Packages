using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Validators.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.Models;
using Regira.Utilities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Entities.Web.Testing;

public class ValidatedOrder : IEntity<int>
{
    public int Id { get; set; }
    public string? Code { get; set; }
    public bool IsLocked { get; set; }
}

public class ValidatedOrderContext(DbContextOptions<ValidatedOrderContext> options) : DbContext(options)
{
    public DbSet<ValidatedOrder> Orders => Set<ValidatedOrder>();
}

/// <summary>Top-level and public because MVC only discovers controllers that are (see IsController).</summary>
[Route("validated-orders")]
public class ValidatedOrdersController : EntityControllerBase<ValidatedOrder>;

/// <summary>
/// Over HTTP, on a host that registers no exception filter (<c>MapEntityExceptions()</c> or
/// <c>ConfigureDefaultJsonOptions()</c>): every generated write answers a validator's rejection with 400 and the flat
/// error map, a field with several messages carrying each of them.
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
            ctx.AddError(nameof(ValidatedOrder.Code), "At most 5 characters.");
            ctx.AddError(nameof(ValidatedOrder.Code), "Upper case only.");
        }
    }

    private sealed class Host(WebApplication app, HttpClient client, SqliteConnection connection, int orderId, int lockedId) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public int OrderId { get; } = orderId;
        public int LockedId { get; } = lockedId;

        public static async Task<Host> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddControllers().AddApplicationPart(typeof(ValidatedOrdersController).Assembly);
            builder.Services.AddDbContext<ValidatedOrderContext>(db => db.UseSqlite(connection));
            builder.Services.AddSingleton<IEntityMapper, JsonMapper>();
            builder.Services.UseEntities<ValidatedOrderContext>(o => o.UseDefaults())
                .For<ValidatedOrder>(e => e.Validate(Validate));

            var app = builder.Build();
            app.MapControllers();

            int orderId, lockedId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<ValidatedOrderContext>();
                await db.Database.EnsureCreatedAsync();
                var order = new ValidatedOrder { Code = "A1" };
                var locked = new ValidatedOrder { Code = "B2", IsLocked = true };
                db.Orders.AddRange(order, locked);
                await db.SaveChangesAsync();
                orderId = order.Id;
                lockedId = locked.Id;
            }

            await app.StartAsync();
            return new Host(app, app.GetTestClient(), connection, orderId, lockedId);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            connection.Close();
        }
    }

    private static async Task<Dictionary<string, string[]>> ErrorsOf(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>())!;
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

        Assert.Equal(["At most 5 characters.", "Upper case only."], (await ErrorsOf(response))["Code"]);
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
}
