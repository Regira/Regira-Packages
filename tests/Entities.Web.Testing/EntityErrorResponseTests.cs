using Entities.Web.Testing.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Mediator;
using Regira.Entities.Mediator.Abstractions;
using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace Entities.Web.Testing;

public class EntityErrorResponseControllerTests(ContosoApiFactory factory) : EntityErrorResponseTests<ContosoApiFactory>(factory)
{
    // the MVC helper reads the body itself; a mapped endpoint leaves a body that is not JSON to the framework's 400
    [Fact]
    public async Task A_Patch_Body_That_Is_Not_Json_Answers_The_400_Of_A_Body_That_Is_No_Json_Object()
    {
        using var client = Factory.CreateClient();
        var id = await CreateDepartment(client);
        var course = await client.PostAsJsonAsync("/courses", new { Title = "Physics 101", DepartmentId = id, Credits = 2 });
        var courseId = JsonDocument.Parse(await course.Content.ReadAsStringAsync()).RootElement.GetProperty("item").GetProperty("id").GetInt32();

        var response = await client.PatchAsync($"/courses/{courseId}", new StringContent("{not json", Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var problem = await Problem(response);
        var detail = problem.GetProperty("errorDetails")[0];
        Assert.Equal("", detail.GetProperty("key").GetString());
        Assert.Equal("A JSON merge patch is a JSON object.", detail.GetProperty("message").GetString());
    }
}
public class EntityErrorResponseEndpointTests(ContosoEndpointsApiFactory factory) : EntityErrorResponseTests<ContosoEndpointsApiFactory>(factory);

/// <summary>
/// The answers around the write pipeline's refusals, on the controllers and the mapped endpoints alike: a 409 carries
/// what the app's problem factory adds (the traceId that ties it to the write service's warning), and a read is
/// cancelled with its request while a write is not.
/// </summary>
public abstract class EntityErrorResponseTests<TFactory> : IClassFixture<TFactory>, IDisposable
    where TFactory : ContosoApiFactory
{
    protected TFactory Factory { get; }
    protected EntityErrorResponseTests(TFactory factory)
    {
        Factory = factory;
        using var dbContext = factory.CreateDbContext();
        dbContext.Database.EnsureCreated();
    }

    public void Dispose()
    {
        using var dbContext = Factory.CreateDbContext();
        dbContext.Database.EnsureDeleted();
    }

    protected static async Task<int> CreateDepartment(HttpClient client)
    {
        var response = await client.PostAsJsonAsync("/departments", new { Title = "Physics", Budget = 2000, StartDate = DateTime.Today });
        response.EnsureSuccessStatusCode();
        return JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement.GetProperty("item").GetProperty("id").GetInt32();
    }

    protected static async Task<JsonElement> Problem(HttpResponseMessage response)
        => JsonDocument.Parse(await response.Content.ReadAsStringAsync()).RootElement;

    private HttpClient ClientWithProblemCustomization() => Factory
        .WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
            services.AddProblemDetails(o => o.CustomizeProblemDetails = ctx => ctx.ProblemDetails.Extensions["app"] = "custom")))
        .CreateClient();

    [Fact]
    public async Task A_Constraint_Conflict_Carries_The_Apps_Problem_Fields()
    {
        using var client = ClientWithProblemCustomization();

        // a course of a department that does not exist: the foreign key refuses it
        var response = await client.PostAsJsonAsync("/courses", new { Title = "Physics 101", DepartmentId = 9999, Credits = 2 });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await Problem(response);
        Assert.Equal("Conflict", problem.GetProperty("title").GetString());
        Assert.Equal("A database constraint rejected the change.", problem.GetProperty("detail").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("type").GetString()));
        Assert.Equal("custom", problem.GetProperty("app").GetString());
    }

    [Fact]
    public async Task A_Concurrency_Conflict_Carries_The_Apps_Problem_Fields()
    {
        using var client = ClientWithProblemCustomization();
        var id = await CreateDepartment(client);

        // a token the row does not hold: the write was built on another read
        var response = await client.PutAsJsonAsync($"/departments/{id}",
            new { Id = id, Title = "Stale edit", Budget = 2000, StartDate = DateTime.Today, ConcurrencyToken = Guid.NewGuid() });

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await Problem(response);
        Assert.Equal("Concurrency conflict", problem.GetProperty("title").GetString());
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("traceId").GetString()));
        Assert.False(string.IsNullOrEmpty(problem.GetProperty("type").GetString()));
        Assert.Equal("custom", problem.GetProperty("app").GetString());
    }

    [Fact]
    public async Task A_Read_Is_Sent_With_The_Requests_Cancellation_And_A_Write_Is_Not()
    {
        var recorder = new TokenRecorder();
        using var app = Factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.AddSingleton(recorder);
            services.AddScoped<IEntitySender, RecordingSender>();
        }));
        using var client = app.CreateClient();

        var id = await CreateDepartment(client);
        (await client.GetAsync($"/departments/{id}")).EnsureSuccessStatusCode();
        (await client.GetAsync("/departments")).EnsureSuccessStatusCode();
        (await client.GetAsync("/departments/search")).EnsureSuccessStatusCode();

        // a download: the upload is a write, both downloads (by id and by name) read through AttachmentFileQuery
        var course = await client.PostAsJsonAsync("/courses", new { Title = "Physics 101", DepartmentId = id, Credits = 2 });
        var courseId = JsonDocument.Parse(await course.Content.ReadAsStringAsync()).RootElement.GetProperty("item").GetProperty("id").GetInt32();
        var upload = await client.PostAsync($"/courses/{courseId}/files",
            new MultipartFormDataContent { { new StringContent("syllabus"), "file", "syllabus.txt" } });
        upload.EnsureSuccessStatusCode();
        var linkId = JsonDocument.Parse(await upload.Content.ReadAsStringAsync()).RootElement.GetProperty("item").GetProperty("id").GetInt32();
        Assert.False(recorder.Cancellable["UploadAttachmentCommand"]);
        recorder.Cancellable.TryRemove("AttachmentFileQuery", out _);
        Assert.Equal("syllabus", await client.GetStringAsync($"/courses/files/{linkId}"));
        Assert.True(recorder.Cancellable["AttachmentFileQuery"]);
        recorder.Cancellable.TryRemove("AttachmentFileQuery", out _);
        Assert.Equal("syllabus", await client.GetStringAsync($"/courses/{courseId}/files/syllabus.txt"));
        Assert.True(recorder.Cancellable["AttachmentFileQuery"]);

        Assert.False(recorder.Cancellable["SaveCommand"]);
        Assert.True(recorder.Cancellable["DetailsQuery"]);
        Assert.True(recorder.Cancellable["ListQuery"]);
        Assert.True(recorder.Cancellable["SearchQuery"]);
    }

    private sealed class TokenRecorder
    {
        public ConcurrentDictionary<string, bool> Cancellable { get; } = new();
    }
    private sealed class RecordingSender(IServiceProvider services, TokenRecorder recorder) : IEntitySender
    {
        private readonly EntitySender _inner = new(services);
        public Task<TResponse?> Send<TResponse>(IEntityRequest<TResponse> request, CancellationToken token = default)
        {
            recorder.Cancellable[request.GetType().Name.Split('`')[0]] = token.CanBeCanceled;
            return _inner.Send(request, token);
        }
    }
}
