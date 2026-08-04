using Entities.TestApi.Infrastructure;
using Entities.TestApi.Infrastructure.Courses;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Web.Models;
using System.Net;
using System.Net.Http.Json;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;

[Collection(nameof(NonParallelCollectionDefinition))]
public class CourseControllerTests : IDisposable
{
    private readonly ContosoContext _dbContext;
    public CourseControllerTests()
    {
        _dbContext = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(ApiConfiguration.ConnectionString).Options);
        _dbContext.Database.EnsureCreated();

        _dbContext.Departments.AddRange(Enumerable.Range(1, 5).Select((_, i) => new Department { Title = $"Department #{i}", Budget = i * 1000, StartDate = DateTime.Today.AddDays(i * 3) }));
        _dbContext.SaveChanges();
    }


    [Fact]
    public async Task Empty_Get()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var response = await client.GetAsync("/courses");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);

        var result = await response.Content.ReadFromJsonAsync<ListResult<CourseDto>>();
        Assert.NotNull(result!.Items);
        Assert.Empty(result.Items);
    }
    [Fact]
    public async Task Get_404()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var courseInput = new CourseInputDto
        {
            Title = "Course (test)",
            DepartmentId = 1,
            Credits = 2
        };
        var inputResponse = await client.PostAsJsonAsync("/courses", courseInput);
        inputResponse.EnsureSuccessStatusCode();

        var response1 = await client.GetAsync("/courses/1");
        Assert.Equal(HttpStatusCode.OK, response1.StatusCode);
        var response2 = await client.GetAsync("/courses/2");
        Assert.Equal(HttpStatusCode.NotFound, response2.StatusCode);
        var response3 = await client.GetAsync("/courses/new");
        Assert.Equal(HttpStatusCode.BadRequest, response3.StatusCode);
    }
    [Fact]
    public async Task Insert_And_Get_Details()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        await CreateTestItems(_dbContext);

        var courseInput = new CourseInputDto
        {
            Title = "Course (test)",
            DepartmentId = 1,
            Credits = 2
        };
        var inputResponse = await client.PostAsJsonAsync("/courses", courseInput);
        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);

        var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();
        Assert.NotNull(saveResult);
        Assert.NotNull(saveResult.Item);
        Assert.True(saveResult.IsNew);
        Assert.Equal(courseInput.Title, saveResult.Item.Title);
        Assert.Equal(courseInput.DepartmentId, saveResult.Item.DepartmentId);
        Assert.Equal(courseInput.Credits, saveResult.Item.Credits);

        var detailsResponse = await client.GetAsync($"/courses/{saveResult.Item.Id}");
        detailsResponse.EnsureSuccessStatusCode();
        var detailsResult = await detailsResponse.Content.ReadFromJsonAsync<DetailsResult<CourseDto>>();
        Assert.NotNull(detailsResult!.Item);
        Assert.NotEqual(0, detailsResult.Item.Id);
        Assert.Equal(saveResult.Item.Id, detailsResult.Item.Id);
    }
    [Fact]
    public async Task Insert_And_Get_List()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var inputCourses = Enumerable.Range(1, 100)
            .Select((_, i) => new CourseInputDto
            {
                Title = $"Course (test-{i.ToString().PadLeft(3, '0')})",
                DepartmentId = Random.Shared.Next(1, 5),
                Credits = (i % 5) + 1
            })
            .ToArray();
        foreach (var courseInput in inputCourses)
        {
            var inputResponse = await client.PostAsJsonAsync("/courses", courseInput);
            Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);
            var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();
            courseInput.Id = saveResult!.Item.Id;
        }

        var listResponse = await client.GetAsync("/courses");
        var listResult = await listResponse.Content.ReadFromJsonAsync<ListResult<CourseDto>>();
        Assert.Equal(inputCourses.Length, listResult!.Items.Count);
        foreach (var courseInput in inputCourses)
        {
            var course = listResult.Items.First(x => x.Id == courseInput.Id);
            Assert.Equal(courseInput.Title, course.Title);
            Assert.Equal(courseInput.DepartmentId, course.DepartmentId);
            Assert.Equal(courseInput.Credits, course.Credits);
        }
    }
    [Fact]
    public async Task Insert_And_Force_404()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var courseInput = new Course
        {
            Title = "Course (test)",
            DepartmentId = 1,
        };
        var inputResponse = await client.PostAsJsonAsync("/courses", courseInput);
        Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);

        var detailsResponse99 = await client.GetAsync("/courses/99");
        Assert.False(detailsResponse99.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse99.StatusCode);

        var detailsResponse0 = await client.GetAsync("/courses/0");
        Assert.False(detailsResponse0.IsSuccessStatusCode);
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse0.StatusCode);
    }
    [Fact]
    public async Task Update()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var courseInput = new CourseInputDto
        {
            Title = "Course (new)",
            DepartmentId = 1,
            Credits = 2
        };
        var insertResponse = await client.PostAsJsonAsync("/courses", courseInput);
        Assert.Equal(HttpStatusCode.OK, insertResponse.StatusCode);

        var insertResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();
        courseInput.Id = insertResult!.Item.Id;

        courseInput.Title = "Course (updated)";
        courseInput.Credits = 4;
        var updateResponse = await client.PutAsJsonAsync($"/courses/{courseInput.Id}", courseInput);

        Assert.Equal(HttpStatusCode.OK, updateResponse.StatusCode);
        var updateResult = await updateResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();

        Assert.NotEqual(insertResult.Item.Title, updateResult!.Item.Title);
        Assert.Equal(courseInput.Credits, updateResult.Item.Credits);

        // also test 'Save' action
        courseInput.Title = "Course (saved)";
        courseInput.Credits = 4;
        var saveResponse = await client.PostAsJsonAsync($"/courses/save", courseInput);

        Assert.Equal(HttpStatusCode.OK, saveResponse.StatusCode);
        var saveResult = await saveResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();

        Assert.NotEqual(updateResult.Item.Title, saveResult!.Item.Title);
        Assert.Equal(courseInput.Id, saveResult.Item.Id);
        Assert.Equal(courseInput.Credits, saveResult.Item.Credits);
    }
    [Fact]
    public async Task Delete()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var courseInput = new CourseInputDto
        {
            Title = "Course (new)",
            DepartmentId = 1,
            Credits = 2
        };
        var insertResponse = await client.PostAsJsonAsync("/courses", courseInput);
        Assert.Equal(HttpStatusCode.OK, insertResponse.StatusCode);

        var insertResult = await insertResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();
        courseInput.Id = insertResult!.Item.Id;

        var deleteResponse = await client.DeleteAsync($"/courses/{courseInput.Id}");

        Assert.Equal(HttpStatusCode.OK, deleteResponse.StatusCode);
        var deleteResult = await deleteResponse.Content.ReadFromJsonAsync<DeleteResult<CourseDto>>();

        Assert.Equal(courseInput.Id, deleteResult!.Item.Id);

        var detailsResponse = await client.GetAsync($"/courses/{courseInput.Id}");
        Assert.Equal(HttpStatusCode.NotFound, detailsResponse.StatusCode);
    }

    [Fact]
    public async Task Insert_And_FilterExclude()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();

        var inputCourses = Enumerable.Range(1, 10)
            .Select((_, i) => new CourseInputDto
            {
                Title = $"Course (test-{i.ToString().PadLeft(3, '0')})",
                DepartmentId = Random.Shared.Next(1, 5),
                Credits = (i % 5) + 1
            })
            .ToArray();
        foreach (var courseInput in inputCourses)
        {
            var inputResponse = await client.PostAsJsonAsync("/courses", courseInput);
            Assert.Equal(HttpStatusCode.OK, inputResponse.StatusCode);
            var saveResult = await inputResponse.Content.ReadFromJsonAsync<SaveResult<CourseDto>>();
            courseInput.Id = saveResult!.Item.Id;
        }

        var listResponse = await client.GetAsync("/courses");
        var listResult = await listResponse.Content.ReadFromJsonAsync<ListResult<CourseDto>>();

        Assert.Equal(listResult!.Items.Count, inputCourses.Length);

        var listResponseExclude = await client.GetAsync("/courses?exclude=2&exclude=3");
        var listResultExclude = await listResponseExclude.Content.ReadFromJsonAsync<ListResult<CourseDto>>();

        Assert.Equal(listResultExclude!.Items.Count, inputCourses.Length - 2);
        var excludedItems = listResultExclude.Items.Where(x => new[] { 2, 3 }.Contains(x.Id));
        Assert.Empty(excludedItems);
    }

    static async Task<IList<Course>> CreateTestItems(ContosoContext dbContext)
    {
        var items = new Course[]
        {
            new () { Title = "Course #1", DepartmentId = 1 },
            new () { Title = "Course #2", DepartmentId = 2 },
            new () { Title = "Course #3", DepartmentId = 3 },
            new () { Title = "Course #4", DepartmentId = 4 },
            new () { Title = "Course #5", DepartmentId = 5 },
        };
        dbContext.Courses.AddRange(items);
        await dbContext.SaveChangesAsync();

        return items;
    }

    public void Dispose()
    {
        _dbContext.Database.EnsureDeleted();
    }
}