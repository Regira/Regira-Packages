using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;

[Collection(nameof(NonParallelCollectionDefinition))]
public class MinimalApiTests : IDisposable
{
    private readonly ContosoContext _dbContext;
    public MinimalApiTests()
    {
        // the same file the API under test writes to — take it from there rather than rebuilding the path
        var filename = Entities.TestApi.Infrastructure.ApiConfiguration.DatabaseFile;
        _dbContext = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite($"Filename={filename}").Options);
        _dbContext.Database.EnsureCreated();
    }

    [Fact]
    public async Task Create_Client()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var response = await client.GetAsync("/");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var content = await response.Content.ReadAsStringAsync();
        Assert.Equal("Hello", content);
    }
    [Fact]
    public async Task Get_404()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var response = await client.GetAsync("does-not-exist");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
    [Fact]
    public async Task Get_Departments()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var response = await client.GetAsync("/minimal/departments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var items = await response.Content.ReadFromJsonAsync<IList<Department>>();
        Assert.NotNull(items);
        Assert.Empty(items);
    }
    [Fact]
    public async Task Add_Departments()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        // UTC kind: the entity pipeline canonicalizes DateTimes to UTC, so a local-kind value would come
        // back as the same instant with shifted ticks and fail the representation equality below
        var dep1 = new Department { Title = "Test_Department", Budget = 3, StartDate = DateTime.UtcNow.Date.AddMonths(1) };
        var inputResponse = await client.PutAsJsonAsync("/minimal/departments", dep1);
        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);

        var response = await client.GetAsync("/minimal/departments");
        var items = await response.Content.ReadFromJsonAsync<IList<Department>>();
        Assert.NotEmpty(items!);
        Assert.Equal(dep1.Title, items![0].Title);
        Assert.Equal(dep1.Budget, items[0].Budget);
        Assert.Equal(dep1.StartDate, items[0].StartDate);
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
    }
}