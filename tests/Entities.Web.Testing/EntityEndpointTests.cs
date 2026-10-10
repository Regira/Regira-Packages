using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Controllers.Abstractions;
using Regira.Entities.Web.DependencyInjection;
using Regira.Entities.Web.Endpoints;
using Regira.Entities.Web.Models;
using Regira.Utilities;

namespace Entities.Web.Testing;

public class Note : IEntity<int>, IArchivable
{
    public int Id { get; set; }
    [MaxLength(20)]
    public string? Title { get; set; }
    public bool IsArchived { get; set; }
}

public class InterventionType : IEntity<int>
{
    public int Id { get; set; }
    public string? Title { get; set; }
}

public class Ticket : IEntity<int>
{
    public int Id { get; set; }
    public string? Title { get; set; }
    public int Priority { get; set; }
}
public record TicketSearchObject : SearchObject
{
    public int? MinPriority { get; set; }
}
public enum TicketSortBy { Title, Priority, PriorityDesc }
[Flags]
public enum TicketIncludes { None = 0, Default = 1 }

/// <summary>Registered without DTOs.</summary>
public class Orphan : IEntity<int>
{
    public int Id { get; set; }
}

public class EndpointContext(DbContextOptions<EndpointContext> options) : DbContext(options)
{
    public DbSet<Note> Notes => Set<Note>();
    public DbSet<InterventionType> InterventionTypes => Set<InterventionType>();
    public DbSet<Ticket> Tickets => Set<Ticket>();
    public DbSet<Orphan> Orphans => Set<Orphan>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);
        modelBuilder.SetArchivedQueryFilter();
    }
}

/// <summary>Top-level and public because MVC only discovers controllers that are.</summary>
[Route("notes")]
public class NotesController : EntityControllerBase<Note>;

/// <summary>
/// What the mapped endpoints do beyond the controllers' routes, which <see cref="CourseEndpointTests"/> and
/// <see cref="CourseAttachmentsEndpointTests"/> mirror: a host without MVC, the query binding, the options on a
/// registration, the DTO rule, the exception filter on an app's own endpoint, the metadata and the startup checks.
/// </summary>
public class EntityEndpointTests
{
    private sealed class JsonMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => JsonSerializer.Deserialize<TTarget>(JsonSerializer.Serialize(source))!;
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
    }

    public sealed record Marker;

    private sealed class Host(WebApplication app, SqliteConnection connection) : IAsyncDisposable
    {
        public WebApplication App { get; } = app;
        public HttpClient Client { get; } = app.GetTestClient();

        /// <param name="disableOrphan">Whether the entity without DTOs is kept off the surface; mapping refuses it otherwise.</param>
        public static async Task<Host> CreateAsync(Action<IServiceCollection>? services = null, Action<WebApplication>? map = null, bool disableOrphan = true)
        {
            var connection = new SqliteConnection("Filename=:memory:");
            connection.Open();

            var builder = WebApplication.CreateBuilder();
            builder.WebHost.UseTestServer();
            builder.Services.ConfigureDefaultJsonOptions();
            builder.Services.AddDbContext<EndpointContext>(db => db.UseSqlite(connection));
            builder.Services.AddSingleton<IEntityMapper, JsonMapper>();
            builder.Services
                .UseEntities<EndpointContext>(o => o.UseDefaults())
                .For<Note>(e => e
                    .Endpoints(o => o.UseDtos<Note, Note>().AllowAnonymous(EntityEndpoint.Details))
                    .Validate(ctx =>
                    {
                        if (ctx.Item.Title == "refused")
                        {
                            ctx.AddError(nameof(Note.Title), "Refused.");
                        }
                    }))
                .For<InterventionType>(e => e.Endpoints(o => o.UseDtos<InterventionType, InterventionType>().Exclude(EntityEndpoint.Delete)))
                .For<Ticket, TicketSearchObject, TicketSortBy, TicketIncludes>(e => e
                    .Endpoints(o => o.UseDtos<Ticket, Ticket>())
                    .Filter((query, so) => so?.MinPriority != null ? query.Where(x => x.Priority >= so.MinPriority) : query)
                    .SortBy((query, sortBy) => sortBy switch
                    {
                        TicketSortBy.Priority => query.OrderBy(x => x.Priority),
                        TicketSortBy.PriorityDesc => query.OrderByDescending(x => x.Priority),
                        _ => query.OrderBy(x => x.Title)
                    }))
                .For<Orphan>(e =>
                {
                    if (disableOrphan)
                    {
                        e.Endpoints(o => o.Disable());
                    }
                });
            services?.Invoke(builder.Services);

            var app = builder.Build();
            if (map != null)
            {
                map(app);
            }
            else
            {
                app.MapEntityEndpoints();
            }

            using (var scope = app.Services.CreateScope())
            {
                var db = scope.ServiceProvider.GetRequiredService<EndpointContext>();
                await db.Database.EnsureCreatedAsync();
                db.Notes.AddRange(new Note { Title = "live" }, new Note { Title = "archived", IsArchived = true }, new Note { Title = "third" });
                db.InterventionTypes.Add(new InterventionType { Title = "Repair" });
                db.Tickets.AddRange(new Ticket { Title = "a", Priority = 1 }, new Ticket { Title = "b", Priority = 3 }, new Ticket { Title = "c", Priority = 2 });
                await db.SaveChangesAsync();
            }

            await app.StartAsync();
            return new Host(app, connection);
        }

        public IEnumerable<RouteEndpoint> Endpoints
            => App.Services.GetRequiredService<EndpointDataSource>().Endpoints.OfType<RouteEndpoint>();

        public RouteEndpoint Endpoint(string method, string pattern)
            => Endpoints.Single(e => e.RoutePattern.RawText?.TrimStart('/') == pattern
                && e.Metadata.GetMetadata<HttpMethodMetadata>()?.HttpMethods.Contains(method) == true);

        public async ValueTask DisposeAsync()
        {
            Client.Dispose();
            await App.DisposeAsync();
            connection.Close();
        }
    }

    private static async Task<JsonElement> Problem(HttpResponseMessage response)
    {
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;
    }

    [Fact]
    public async Task A_Host_Without_Mvc_Serves_The_Routes_Of_The_Controllers()
    {
        await using var host = await Host.CreateAsync();
        var client = host.Client;

        var created = await (await client.PostAsJsonAsync("/notes", new Note { Title = "new" })).Content.ReadFromJsonAsync<SaveResult<Note>>();
        var id = created!.Item.Id;
        var modified = await client.PutAsJsonAsync($"/notes/{id}", new Note { Title = "modified" });
        var patched = await client.PatchAsync($"/notes/{id}", JsonContent.Create(new { title = "patched" }));
        var details = await client.GetFromJsonAsync<DetailsResult<Note>>($"/notes/{id}");
        var search = await client.GetFromJsonAsync<SearchResult<Note>>("/notes/search?pageSize=1");
        var deleted = await client.DeleteAsync($"/notes/{id}");
        var afterDelete = await client.GetAsync($"/notes/{id}");

        Assert.True(created.IsNew);
        Assert.NotNull(created.Duration);
        Assert.Equal(HttpStatusCode.OK, modified.StatusCode);
        Assert.Equal(HttpStatusCode.OK, patched.StatusCode);
        Assert.Equal("patched", details!.Item.Title);
        Assert.Equal(3, search!.Count);
        Assert.Single(search.Items);
        Assert.Equal(HttpStatusCode.OK, deleted.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, afterDelete.StatusCode);
        Assert.Null(host.App.Services.GetService<Microsoft.AspNetCore.Mvc.Infrastructure.ProblemDetailsFactory>());
    }

    [Theory]
    [InlineData("", new[] { "live", "third" })]
    [InlineData("?archived=included", new[] { "live", "archived", "third" })]
    [InlineData("?archived=ONLY", new[] { "archived" })]
    public async Task The_Archived_Filter_Binds_On_The_Search_Object(string query, string[] expected)
    {
        await using var host = await Host.CreateAsync();

        var list = await host.Client.GetFromJsonAsync<ListResult<Note>>($"/notes{query}");

        Assert.Equal(expected.Order(), list!.Items.Select(x => x.Title).Order());
    }

    [Fact]
    public async Task Details_Reaches_An_Archived_Row_Only_When_Asked()
    {
        await using var host = await Host.CreateAsync();
        var archivedId = (await host.Client.GetFromJsonAsync<ListResult<Note>>("/notes?archived=only"))!.Items.Single().Id;

        var hidden = await host.Client.GetAsync($"/notes/{archivedId}");
        var shown = await host.Client.GetAsync($"/notes/{archivedId}?archived=Included");

        Assert.Equal(HttpStatusCode.NotFound, hidden.StatusCode);
        Assert.Equal(HttpStatusCode.OK, shown.StatusCode);
    }

    [Theory]
    [InlineData("?ids={0}&ids={1}")]
    [InlineData("?ids[0]={0}&ids[1]={1}")]
    public async Task A_Collection_Binds_From_Repeated_And_Indexed_Keys(string query)
    {
        await using var host = await Host.CreateAsync();
        var all = (await host.Client.GetFromJsonAsync<ListResult<Note>>("/notes"))!.Items;

        var list = await host.Client.GetFromJsonAsync<ListResult<Note>>("/notes" + string.Format(query, all[0].Id, all[1].Id));
        var excluded = await host.Client.GetFromJsonAsync<ListResult<Note>>($"/notes?exclude={all[0].Id}");

        Assert.Equal(new[] { all[0].Id, all[1].Id }, list!.Items.Select(x => x.Id).Order());
        Assert.DoesNotContain(all[0].Id, excluded!.Items.Select(x => x.Id));
    }

    [Fact]
    public async Task A_Value_That_Does_Not_Convert_Is_A_400_Keyed_By_Its_Name()
    {
        await using var host = await Host.CreateAsync();

        var list = await Problem(await host.Client.GetAsync("/notes?ids=abc"));
        var details = await Problem(await host.Client.GetAsync("/notes/1?archived=sometimes"));
        var includes = await Problem(await host.Client.GetAsync("/tickets?includes=Everything"));

        Assert.True(list.GetProperty("errors").TryGetProperty("Ids", out _));
        Assert.Equal("Ids", list.GetProperty("errorDetails")[0].GetProperty("key").GetString());
        Assert.True(details.GetProperty("errors").TryGetProperty("archived", out _));
        Assert.True(includes.GetProperty("errors").TryGetProperty("includes", out _));
    }

    [Fact]
    public async Task A_Mapped_Write_Checks_Its_Input_Against_Its_DataAnnotations()
    {
        await using var host = await Host.CreateAsync();

        var problem = await Problem(await host.Client.PostAsJsonAsync("/notes", new Note { Title = new string('x', 21) }));

        Assert.True(problem.GetProperty("errors").TryGetProperty("Title", out _));
    }

    [Fact]
    public async Task A_Refused_Write_Answers_400_And_The_Apps_Problem_Customization_Applies_Without_Mvc()
    {
        await using var host = await Host.CreateAsync(services =>
            services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Extensions["app"] = "custom"));

        var refused = await Problem(await host.Client.PostAsJsonAsync("/notes", new Note { Title = "refused" }));
        var notFound = await host.Client.GetAsync("/notes/999");

        Assert.Equal("Refused.", refused.GetProperty("errorDetails")[0].GetProperty("message").GetString());
        Assert.Equal("custom", refused.GetProperty("app").GetString());
        Assert.Equal(HttpStatusCode.NotFound, notFound.StatusCode);
        Assert.Contains("custom", await notFound.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task A_Complex_Entity_Binds_Its_Search_Object_Sort_And_Includes()
    {
        await using var host = await Host.CreateAsync();

        var sorted = await host.Client.GetFromJsonAsync<ListResult<Ticket>>("/tickets?minPriority=2&sortBy=prioritydesc&includes=Default");
        var search = await host.Client.GetFromJsonAsync<SearchResult<Ticket>>("/tickets/search?sortBy=Priority");
        var bodies = await host.Client.PostAsJsonAsync("/tickets/list?sortBy=Priority", new object[] { new { minPriority = 3 }, new { ids = new[] { 1 } } });

        Assert.Equal(new[] { 3, 2 }, sorted!.Items.Select(x => x.Priority));
        Assert.Equal(new[] { 1, 2, 3 }, search!.Items.Select(x => x.Priority));
        Assert.Equal(3, search.Count);
        Assert.Equal(new[] { 1, 3 }, (await bodies.Content.ReadFromJsonAsync<ListResult<Ticket>>())!.Items.Select(x => x.Priority));
    }

    [Fact]
    public async Task The_Route_Is_The_Kebab_Case_Plural_And_The_Set_Can_Be_Trimmed()
    {
        await using var host = await Host.CreateAsync();

        var list = await host.Client.GetAsync("/intervention-types");
        var delete = await host.Client.DeleteAsync("/intervention-types/1");

        Assert.Equal(HttpStatusCode.OK, list.StatusCode);
        Assert.Equal(HttpStatusCode.MethodNotAllowed, delete.StatusCode);
    }

    [Fact]
    public async Task An_Entity_Without_Dtos_Is_Refused_At_Mapping_Unless_Disabled()
    {
        await using (var host = await Host.CreateAsync())
        {
            Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/orphans")).StatusCode);
        }

        var ex = await Assert.ThrowsAsync<InvalidOperationException>(() => Host.CreateAsync(disableOrphan: false));

        Assert.Contains("Orphan", ex.Message);
        Assert.Contains("UseMapping<TDto, TInputDto>()", ex.Message);
    }

    [Fact]
    public async Task MapEntity_Maps_One_Entity_On_A_Route_Of_Choice()
    {
        await using var host = await Host.CreateAsync(map: app => app.MapEntity<Note>("v2/notes"));

        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/v2/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await host.Client.GetAsync("/tickets")).StatusCode);
    }

    [Fact]
    public async Task An_Apps_Own_Endpoint_Takes_The_Exception_Filter()
    {
        await using var host = await Host.CreateAsync(map: app =>
        {
            app.MapEntityEndpoints();
            app.MapPost("/notes/import", async (IEntitySender sender, Note note)
                    => Results.Ok(await sender.Send(new SaveCommand<Note, int, Note, Note>(note))))
                .AddEndpointFilter<EntityExceptionEndpointFilter>();
        });

        var problem = await Problem(await host.Client.PostAsJsonAsync("/notes/import", new Note { Title = "refused" }));

        Assert.Equal("Title", problem.GetProperty("errorDetails")[0].GetProperty("key").GetString());
    }

    [Fact]
    public async Task Every_Endpoint_Carries_Its_Entity_And_Whether_It_Writes()
    {
        await using var host = await Host.CreateAsync(map: app => app.MapEntityEndpoints(o => o.ConfigureGroup<Note>(g => g.WithMetadata(new Marker()))));

        EntityEndpointMetadata? Metadata(string method, string pattern) => host.Endpoint(method, pattern).Metadata.GetMetadata<EntityEndpointMetadata>();

        Assert.Equal(new EntityEndpointMetadata(typeof(Ticket), EntityEndpoint.List), Metadata("POST", "tickets/list"));
        Assert.False(Metadata("POST", "tickets/list")!.IsWrite);
        Assert.True(Metadata("DELETE", "tickets/{id}")!.IsWrite);
        var noteDetails = host.Endpoint("GET", "notes/{id}");
        Assert.NotNull(noteDetails.Metadata.GetMetadata<IAllowAnonymous>());
        Assert.NotNull(noteDetails.Metadata.GetMetadata<Marker>());
        Assert.All(host.Endpoints.Where(e => e.RoutePattern.RawText!.TrimStart('/').StartsWith("tickets")), e => Assert.Null(e.Metadata.GetMetadata<Marker>()));
    }

    [Fact]
    public async Task The_Startup_Checks_See_The_Mapped_Dtos_And_An_Entity_Served_Twice()
    {
        await using var host = await Host.CreateAsync(
            services => services.AddControllers().AddApplicationPart(typeof(NotesController).Assembly),
            app =>
            {
                app.MapControllers();
                app.MapEntityEndpoints();          // leaves Note to its controller
                app.MapEntity<Note>("v2/notes");   // maps it regardless
            });
        var services = host.App.Services;
        var context = new EntityValidationContext(services, services.GetRequiredService<IServiceCollection>(), services.GetRequiredService<EntityRegistrationLog>());

        var shapes = services.GetServices<IEntityDtoShapeSource>().SelectMany(s => s.GetDtoShapes()).ToList();
        var issues = services.GetServices<IEntityRegistrationValidator>().SelectMany(v => v.Validate(context)).ToList();

        Assert.Contains(shapes, s => s.EntityType == typeof(Ticket) && s.InputDtoType == typeof(Ticket));
        Assert.Contains(issues, i => i.Severity == EntityValidationSeverity.Warning && i.Message.Contains("Served by a controller and by mapped endpoints: Note"));
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/notes")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await host.Client.GetAsync("/v2/notes")).StatusCode);
    }
}

/// <summary>
/// The write-authorization recipe for the mapped endpoints (entities.patterns → Role-gated write authorization filter):
/// a policy on the mapped group whose handler reads <see cref="EntityEndpointMetadata"/>, and fails closed.
/// </summary>
public class EntityEndpointAuthorizationTests
{
    private sealed class JsonMapper : IEntityMapper
    {
        public TTarget Map<TTarget>(object source) => JsonSerializer.Deserialize<TTarget>(JsonSerializer.Serialize(source))!;
        public TTarget Map<TSource, TTarget>(TSource source, TTarget target) => ObjectUtility.Fill(target, source!);
    }

    /// <summary>Signs a caller in with the roles its X-Roles header names; no header, no identity.</summary>
    private sealed class HeaderAuthentication(IOptionsMonitor<AuthenticationSchemeOptions> options,
        ILoggerFactory logger, UrlEncoder encoder)
        : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
    {
        protected override Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            if (!Request.Headers.TryGetValue("X-Roles", out var roles))
            {
                return Task.FromResult(AuthenticateResult.NoResult());
            }
            var claims = roles.ToString().Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(r => new Claim(ClaimTypes.Role, r))
                .Append(new Claim(ClaimTypes.Name, "user"));
            var principal = new ClaimsPrincipal(new ClaimsIdentity(claims, Scheme.Name));
            return Task.FromResult(AuthenticateResult.Success(new AuthenticationTicket(principal, Scheme.Name)));
        }
    }

    public sealed class EntityWriteRequirement : IAuthorizationRequirement;

    // the recipe's handler, with IsInRole where an app reads its roles through FindRoles()
    public sealed class EntityWriteAuthorizationHandler : AuthorizationHandler<EntityWriteRequirement>
    {
        private static readonly Dictionary<Type, string[]> WriteRoles = new()
        {
            [typeof(Note)] = ["Editor"],
        };

        protected override Task HandleRequirementAsync(AuthorizationHandlerContext context, EntityWriteRequirement requirement)
        {
            var http = context.Resource as HttpContext;
            var metadata = http?.GetEndpoint()?.Metadata.GetMetadata<EntityEndpointMetadata>();
            var isWrite = metadata?.IsWrite ?? !HttpMethods.IsGet(http?.Request.Method ?? string.Empty);
            if (!isWrite
                || metadata != null && WriteRoles.TryGetValue(metadata.EntityType, out var roles) && (roles.Length == 0 || roles.Any(context.User.IsInRole)))
            {
                context.Succeed(requirement);
            }
            return Task.CompletedTask;
        }
    }

    [Fact]
    public async Task Writes_Need_The_Entitys_Role_Before_Any_Binding_And_An_Unlisted_Entity_Is_Refused()
    {
        using var connection = new SqliteConnection("Filename=:memory:");
        connection.Open();
        var builder = WebApplication.CreateBuilder();
        builder.WebHost.UseTestServer();
        builder.Services.AddDbContext<EndpointContext>(db => db.UseSqlite(connection));
        builder.Services.AddSingleton<IEntityMapper, JsonMapper>();
        builder.Services.AddAuthentication("Header").AddScheme<AuthenticationSchemeOptions, HeaderAuthentication>("Header", null);
        builder.Services.AddAuthorization(o => o.AddPolicy("EntityWrites", p => p.RequireAuthenticatedUser().AddRequirements(new EntityWriteRequirement())));
        builder.Services.AddSingleton<IAuthorizationHandler, EntityWriteAuthorizationHandler>();
        builder.Services.UseEntities<EndpointContext>(o => o.UseDefaults())
            .For<Note>(e => e.Endpoints(o => o.UseDtos<Note, Note>().AllowAnonymous(EntityEndpoint.Details)))
            .For<Ticket, TicketSearchObject, TicketSortBy, TicketIncludes>(e => e.Endpoints(o => o.UseDtos<Ticket, Ticket>()))
            .For<InterventionType>(e => e.Endpoints(o => o.Disable()))
            .For<Orphan>(e => e.Endpoints(o => o.Disable()));
        await using var app = builder.Build();
        app.UseAuthentication();
        app.UseAuthorization();
        var group = app.MapEntityEndpoints().RequireAuthorization("EntityWrites");
        group.MapPost("notes/{id}/approve", () => Results.Ok());     // the app's own write on the group, without entity metadata
        using (var scope = app.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<EndpointContext>();
            await db.Database.EnsureCreatedAsync();
            db.Notes.Add(new Note { Title = "live" });
            await db.SaveChangesAsync();
        }
        await app.StartAsync();
        using var client = app.GetTestClient();

        Task<HttpResponseMessage> Send(HttpMethod method, string url, string? roles, HttpContent? content = null)
        {
            var request = new HttpRequestMessage(method, url) { Content = content };
            if (roles != null)
            {
                request.Headers.Add("X-Roles", roles);
            }
            return client.SendAsync(request);
        }
        var malformed = new StringContent("{ not json", System.Text.Encoding.UTF8, "application/json");

        Assert.Equal(HttpStatusCode.Unauthorized, (await Send(HttpMethod.Get, "/notes", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Get, "/notes/1", null)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Get, "/notes", "Reader")).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, "/tickets/search", "Reader", JsonContent.Create(Array.Empty<object>()))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/notes", "Reader", malformed)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Send(HttpMethod.Post, "/notes", "Editor", JsonContent.Create(new Note { Title = "new" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/tickets", "Editor", JsonContent.Create(new Ticket { Title = "t" }))).StatusCode);
        Assert.Equal(HttpStatusCode.Forbidden, (await Send(HttpMethod.Post, "/notes/1/approve", "Editor")).StatusCode);
    }
}

/// <summary>On the test API in endpoint mode, what stays on its controllers and what stays off the surface.</summary>
public class EntityEndpointCoexistenceTests(Infrastructure.ContosoEndpointsApiFactory factory) : IClassFixture<Infrastructure.ContosoEndpointsApiFactory>
{
    [Fact]
    public async Task A_Controllers_Entity_Stays_On_The_Controller_And_A_Disabled_One_Is_Not_Mapped()
    {
        using var db = factory.CreateDbContext();
        await db.Database.EnsureCreatedAsync();
        using var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/departments")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync("/enrollments")).StatusCode);
    }
}
