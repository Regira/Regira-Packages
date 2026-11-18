using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.Endpoints;
using Regira.Entities.Web.Models;
using Regira.Utilities;

namespace Entities.Web.Testing;

public class OwnedRow : IEntity<int>, IArchivable
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public string OwnerId { get; set; } = null!;
    public bool IsArchived { get; set; }
}
public record OwnedRowSearchObject : SearchObject;
public enum OwnedRowSortBy { Title }
[Flags]
public enum OwnedRowIncludes { None = 0 }

public class OwnedRowContext(DbContextOptions<OwnedRowContext> options) : DbContext(options)
{
    public DbSet<OwnedRow> OwnedRows => Set<OwnedRow>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.SetArchivedQueryFilter();
    }
}

/// <summary>The row scope of the guides: a global filter on the caller's id, here read from a header.</summary>
public class OwnedRowScopeFilter(IHttpContextAccessor http) : GlobalFilteredQueryBuilderBase<OwnedRow>
{
    public override IQueryable<OwnedRow> Build(IQueryable<OwnedRow> query, ISearchObject<int>? so)
    {
        var owner = http.HttpContext?.Request.Headers["X-Owner"].ToString();
        return query.Where(x => x.OwnerId == owner);
    }
}

/// <summary>Top-level and public because MVC only discovers controllers that are.</summary>
[Route("owned-rows")]
public class OwnedRowsController : EntityControllerBase<OwnedRow, OwnedRowSearchObject, OwnedRowSortBy, OwnedRowIncludes, OwnedRow, OwnedRow>;

/// <summary>
/// <c>POST list</c> and <c>POST search</c> of a complex entity take an array of search objects: an empty array filters as
/// one without criteria, so the row scope and the archived filter hold, and a long one is refused with 400.
/// </summary>
public class SearchObjectListTests
{
    private sealed class JsonMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => JsonSerializer.Deserialize<TTarget>(JsonSerializer.Serialize(source))!;
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
    }

    private sealed class Host(WebApplication app, SqliteConnection connection) : IAsyncDisposable
    {
        public HttpClient Client { get; } = app.GetTestClient();

        public static async Task<Host> CreateAsync(bool controllers)
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.AddHttpContextAccessor();
            builder.Services.AddDbContext<OwnedRowContext>(db => db.UseSqlite(connection));
            builder.Services.AddSingleton<IEntityMapper, JsonMapper>();
            if (controllers)
            {
                builder.Services.AddControllers().AddApplicationPart(typeof(OwnedRowsController).Assembly);
            }
            builder.Services
                .UseEntities<OwnedRowContext>(o =>
                {
                    o.UseDefaults();
                    o.AddGlobalFilterQueryBuilder<OwnedRowScopeFilter>();
                })
                .For<OwnedRow, OwnedRowSearchObject, OwnedRowSortBy, OwnedRowIncludes>(e =>
                {
                    if (!controllers)
                    {
                        e.Endpoints(o => o.UseDtos<OwnedRow, OwnedRow>());
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

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<OwnedRowContext>();
                await db.Database.EnsureCreatedAsync();
                db.OwnedRows.AddRange(
                    new OwnedRow { Title = "mine", OwnerId = "ann" },
                    new OwnedRow { Title = "mine, archived", OwnerId = "ann", IsArchived = true },
                    new OwnedRow { Title = "theirs", OwnerId = "bob" });
                await db.SaveChangesAsync();
            }

            await app.StartAsync();
            var host = new Host(app, connection);
            host.Client.DefaultRequestHeaders.Add("X-Owner", "ann");
            return host;
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            connection.Close();
        }
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_Empty_Array_Lists_As_Get_Does(bool controllers)
    {
        await using var host = await Host.CreateAsync(controllers);

        var get = await host.Client.GetFromJsonAsync<SearchResult<OwnedRow>>("/owned-rows/search");
        var list = await host.Client.PostAsJsonAsync("/owned-rows/list", Array.Empty<object>());
        var search = await host.Client.PostAsJsonAsync("/owned-rows/search", Array.Empty<object>());

        Assert.Equal(new[] { "mine" }, get!.Items.Select(x => x.Title));
        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(new[] { "mine" }, (await list.Content.ReadFromJsonAsync<ListResult<OwnedRow>>())!.Items.Select(x => x.Title));
        var searched = (await search.Content.ReadFromJsonAsync<SearchResult<OwnedRow>>())!;
        Assert.Equal(new[] { "mine" }, searched.Items.Select(x => x.Title));
        Assert.Equal(1, searched.Count);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task An_Array_Of_Null_Lists_As_Get_Does(bool controllers)
    {
        await using var host = await Host.CreateAsync(controllers);

        var list = await host.Client.PostAsync("/owned-rows/list", new StringContent("[null]", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(new[] { "mine" }, (await list.Content.ReadFromJsonAsync<ListResult<OwnedRow>>())!.Items.Select(x => x.Title));
    }

    // a JSON null body is refused by binding, before any query runs
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task A_Null_Body_Answers_400(bool controllers)
    {
        await using var host = await Host.CreateAsync(controllers);

        var search = await host.Client.PostAsync("/owned-rows/search", new StringContent("null", System.Text.Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, search.StatusCode);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task More_Than_32_Search_Objects_Answer_400(bool controllers)
    {
        await using var host = await Host.CreateAsync(controllers);
        var atMax = Enumerable.Range(0, 32).Select(_ => new { }).ToArray();
        var overMax = Enumerable.Range(0, 33).Select(_ => new { }).ToArray();

        var allowed = await host.Client.PostAsJsonAsync("/owned-rows/search", atMax);
        var list = await host.Client.PostAsJsonAsync("/owned-rows/list", overMax);
        var search = await host.Client.PostAsJsonAsync("/owned-rows/search", overMax);

        Assert.Equal(HttpStatusCode.OK, allowed.StatusCode);
        Assert.Equal(new[] { "mine" }, (await allowed.Content.ReadFromJsonAsync<SearchResult<OwnedRow>>())!.Items.Select(x => x.Title));
        foreach (var response in new[] { list, search })
        {
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
            var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
            Assert.Equal(32, problem.GetProperty("errorDetails")[0].GetProperty("args").GetProperty("max").GetInt32());
        }
    }
}
