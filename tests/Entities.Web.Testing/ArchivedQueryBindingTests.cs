using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.Models;
using Regira.Utilities;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Entities.Web.Testing;

public class BindingDoc : IEntity<int>, IArchivable
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public bool IsArchived { get; set; }
}

public class BindingDocContext(DbContextOptions<BindingDocContext> options) : DbContext(options)
{
    public DbSet<BindingDoc> Docs => Set<BindingDoc>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.SetArchivedQueryFilter();
    }
}

/// <summary>Top-level and public because MVC only discovers controllers that are (see IsController).</summary>
[Route("binding-docs")]
public class BindingDocsController : EntityControllerBase<BindingDoc>;

/// <summary>
/// The wire contract of the archived filter: <c>GET /{id}</c> keeps 404-ing on an archived row,
/// <c>?archived=included</c> / <c>?archived=only</c> resolve it, and the enum binds case-insensitively
/// on both the by-id route and the search object.
/// </summary>
public class ArchivedQueryBindingTests
{
    /// <summary>DTO == entity here, so a JSON round trip is an exact mapper — and it handles list targets.</summary>
    private sealed class JsonMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => JsonSerializer.Deserialize<TTarget>(JsonSerializer.Serialize(source))!;
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
    }

    private sealed class Host(WebApplication app, HttpClient client, SqliteConnection connection, int liveId, int archivedId) : IAsyncDisposable
    {
        public HttpClient Client { get; } = client;
        public int LiveId { get; } = liveId;
        public int ArchivedId { get; } = archivedId;

        public static async Task<Host> CreateAsync()
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            // the entry assembly under the test runner is the test host, so the part carrying
            // BindingDocsController has to be added explicitly
            builder.Services.AddControllers().AddApplicationPart(typeof(BindingDocsController).Assembly);
            builder.Services.AddDbContext<BindingDocContext>(db => db.UseSqlite(connection));
            builder.Services.AddSingleton<IEntityMapper, JsonMapper>();
            builder.Services.UseEntities<BindingDocContext>(o => o.UseDefaults()).For<BindingDoc>();

            var app = builder.Build();
            app.MapControllers();

            int liveId, archivedId;
            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<BindingDocContext>();
                await db.Database.EnsureCreatedAsync();
                var live = new BindingDoc { Title = "live" };
                var archived = new BindingDoc { Title = "archived", IsArchived = true };
                db.Docs.AddRange(live, archived);
                await db.SaveChangesAsync();
                liveId = live.Id;
                archivedId = archived.Id;
            }

            await app.StartAsync();
            return new Host(app, app.GetTestClient(), connection, liveId, archivedId);
        }

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await app.DisposeAsync();
            connection.Close();
        }
    }

    [Fact]
    public async Task Details_Without_The_Parameter_404s_On_An_Archived_Row()
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.GetAsync($"/binding-docs/{host.ArchivedId}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("included")]
    [InlineData("Included")]
    [InlineData("INCLUDED")]
    [InlineData("only")]
    [InlineData("Only")]
    public async Task Details_With_Archived_Resolves_An_Archived_Row(string value)
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.GetAsync($"/binding-docs/{host.ArchivedId}?archived={value}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<DetailsResult<BindingDoc>>();
        Assert.Equal("archived", payload!.Item!.Title);
    }

    [Fact]
    public async Task Details_With_Archived_Only_404s_On_A_Live_Row()
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.GetAsync($"/binding-docs/{host.LiveId}?archived=only");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("", new[] { "live" })]
    [InlineData("?archived=excluded", new[] { "live" })]
    [InlineData("?archived=included", new[] { "live", "archived" })]
    [InlineData("?archived=only", new[] { "archived" })]
    [InlineData("?archived=ONLY", new[] { "archived" })]
    public async Task List_Binds_Archived_On_The_Search_Object(string query, string[] expected)
    {
        await using var host = await Host.CreateAsync();

        var response = await host.Client.GetAsync($"/binding-docs{query}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var payload = await response.Content.ReadFromJsonAsync<ListResult<BindingDoc>>();
        Assert.Equal(expected.Order(), payload!.Items!.Select(x => x.Title).Order());
    }
}
