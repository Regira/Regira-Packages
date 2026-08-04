using System.Net;
using System.Net.Http.Json;
using Entities.TestApi.Infrastructure;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Web.Models;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;

[Collection(nameof(NonParallelCollectionDefinition))]
public class DepartmentControllerTests : IDisposable
{
    private readonly ContosoContext _dbContext;
    public DepartmentControllerTests()
    {
        _dbContext = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(ApiConfiguration.ConnectionString).Options);
        _dbContext.Database.EnsureCreated();

        _dbContext.SaveChanges();
    }


    [Fact]
    public async Task Empty_Get()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var response = await client.GetAsync("/departments");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ListResult<Department>>();
        Assert.NotNull(result!.Items);
        Assert.Empty(result.Items);
    }
    [Fact]
    public async Task Get_404()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var departmentInput = new Department
        {
            Title = "Department (test)",
            Budget = 2000
        };
        var inputResponse = await client.PostAsJsonAsync("/departments", departmentInput);
        inputResponse.EnsureSuccessStatusCode();

        var response1 = await client.GetAsync("/departments/1");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var response2 = await client.GetAsync("/departments/2");
        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
        var response3 = await client.GetAsync("/departments/new");
        Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
    }
    [Fact]
    public async Task Insert_And_Get_Details()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        await CreateTestItems(_dbContext);

        var departmentInput = new Department
        {
            Title = "Department (test)",
            Budget = 2000
        };
        var inputResponse = await client.PostAsJsonAsync("/departments", departmentInput);

        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);

        var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<Department>>();

        Assert.NotNull(saveResult);
        Assert.NotNull(saveResult.Item);
        Assert.True(saveResult.IsNew);
        Assert.Equal(departmentInput.Title, saveResult.Item.Title);
        Assert.Equal(departmentInput.Budget, saveResult.Item.Budget);

        var detailsResponse = await client.GetAsync($"/departments/{saveResult.Item.Id}");
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<Department>>();
        Assert.NotNull(detailsResult!.Item);
        Assert.NotEqual(0, detailsResult.Item.Id);
        Assert.Equal(saveResult.Item.Id, detailsResult.Item.Id);
    }
    [Fact]
    public async Task Insert_And_Get_List()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var inputDepartments = Enumerable.Range(1, 100)
            .Select((_, i) => new Department
            {
                Title = $"Department (test-{i.ToString().PadLeft(3, '0')})",
                Budget = i * 500
            })
            .ToArray();
        foreach (var departmentInput in inputDepartments)
        {
            var inputResponse = await client.PostAsJsonAsync("/departments", departmentInput);
            Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);
            var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<Department>>();
            departmentInput.Id = saveResult!.Item.Id;
        }

        var listResponse = await client.GetAsync("/departments");
        var listResult = await listResponse.Content.ReadFromJsonAsync<ListResult<Department>>();
        Assert.Equal(inputDepartments.Length, listResult!.Items.Count);
        foreach (var departmentInput in inputDepartments)
        {
            var department = listResult.Items.First(x => x.Id == departmentInput.Id);
            Assert.Equal(departmentInput.Title, department.Title);
            Assert.Equal(departmentInput.Budget, department.Budget);
        }
    }
    [Fact]
    public async Task Insert_And_Force_404()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var departmentInput = new Department
        {
            Title = "Department (test)",
            Budget = 2000
        };
        var inputResponse = await client.PostAsJsonAsync("/departments", departmentInput);
        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);

        var detailsResponse99 = await client.GetAsync("/departments/99");
        Assert.False(detailsResponse99.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse99.StatusCode);

        var detailsResponse0 = await client.GetAsync("/departments/0");
        Assert.False(detailsResponse0.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse0.StatusCode);
    }
    [Fact]
    public async Task Update()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var departmentInput = new Department
        {
            Title = "Department (new)",
            Budget = 2000
        };
        var insertResponse = await client.PostAsJsonAsync("/departments", departmentInput);
        Assert.Equal(HttpStatusCode.OK, insertResponse.StatusCode);

        var insertResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<Department>>();
        departmentInput.Id = insertResult!.Item.Id;

        departmentInput.Title = "Department (updated)";
        departmentInput.Budget *= 2;
        var updateResponse = await client.PutAsJsonAsync($"/departments/{departmentInput.Id}", departmentInput);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<SaveResult<Department>>();

        Assert.NotEqual(insertResult.Item.Title, updateResult!.Item.Title);
        Assert.Equal(departmentInput.Budget, updateResult.Item.Budget);
    }
    [Fact]
    public async Task Delete()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var departmentInput = new Department
        {
            Title = "Department (new)",
            Budget = 2000
        };
        var insertResponse = await client.PostAsJsonAsync("/departments", departmentInput);
        Assert.Equal(HttpStatusCode.OK, insertResponse.StatusCode);

        var insertResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<Department>>();
        departmentInput.Id = insertResult!.Item.Id;

        var deleteResponse = await client.DeleteAsync($"/departments/{departmentInput.Id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<DeleteResult<Department>>();

        Assert.Equal(departmentInput.Id, deleteResult!.Item.Id);

        var detailsResponse = await client.GetAsync($"/departments/{departmentInput.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);
    }

    static async Task<IList<Department>> CreateTestItems(ContosoContext dbContext)
    {
        var items = new Department[]
        {
            new () { Title = "Department #1", Budget = 1000 },
            new () { Title = "Department #2", Budget = 2000 },
            new () { Title = "Department #3", Budget = 3000 },
            new () { Title = "Department #4", Budget = 4000 },
            new () { Title = "Department #5", Budget = 5000 },
        };
        dbContext.Departments.AddRange(items);
        await dbContext.SaveChangesAsync();

        return items;
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
    }
}