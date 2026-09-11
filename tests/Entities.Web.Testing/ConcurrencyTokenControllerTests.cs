using System.Net;
using System.Net.Http.Json;
using Entities.TestApi.Infrastructure;
using Entities.TestApi.Infrastructure.Departments;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.Web.Models;
using Testing.Library.Data;

namespace Entities.Web.Testing;

/// <summary>
/// Optimistic concurrency end to end. <c>Department</c> implements <c>IHasConcurrencyToken</c>: the TestApi's
/// <c>UseDefaults()</c> declares the token and mints a new one on every save, and both DTOs carry it. Where a test
/// needs another writer it uses this fixture's own <see cref="ContosoContext"/>, which moves the token directly.
/// Each refusal is paired with the same request carrying the current token.
/// </summary>
[Collection(nameof(NonParallelCollectionDefinition))]
public class ConcurrencyTokenControllerTests : IDisposable
{
    private readonly ContosoContext _dbContext;
    public ConcurrencyTokenControllerTests()
    {
        _dbContext = new ContosoContext(new DbContextOptionsBuilder<ContosoContext>().UseSqlite(ApiConfiguration.ConnectionString).Options);
        _dbContext.Database.EnsureCreated();
    }

    public void Dispose() => _dbContext.Database.EnsureDeleted();

    private static DepartmentInputDto Input(string title, Guid token, int id = 0)
        => new() { Id = id, Title = title, Budget = 2000, StartDate = DateTime.Today, ConcurrencyToken = token };

    private static async Task<DepartmentDto> Create(HttpClient client, Guid token)
    {
        var response = await client.PostAsJsonAsync("/departments", Input("Physics", token));
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<SaveResult<DepartmentDto>>())!.Item;
    }

    /// Another writer saves the row after the client read it, and moves its token.
    private async Task<Guid> OtherWriter(int id)
    {
        var row = await _dbContext.Departments.SingleAsync(x => x.Id == id);
        row.Title = "Edited elsewhere";
        row.ConcurrencyToken = Guid.NewGuid();
        await _dbContext.SaveChangesAsync();
        return row.ConcurrencyToken;
    }

    [Fact]
    public async Task Each_Save_Returns_A_New_Token_And_A_Client_Still_Holding_The_Old_One_Is_Refused()
    {
        // two clients read the same department; the first one's save moves the token under the second
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var created = await Create(client, Guid.Empty);
        var read = created.ConcurrencyToken;

        var first = await client.PutAsJsonAsync($"/departments/{created.Id}", Input("First edit", read, created.Id));
        var second = await client.PutAsJsonAsync($"/departments/{created.Id}", Input("Second edit", read, created.Id));

        Assert.NotEqual(Guid.Empty, read);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        var saved = (await first.Content.ReadFromJsonAsync<SaveResult<DepartmentDto>>())!.Item;
        Assert.NotEqual(read, saved.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
    }

    [Fact]
    public async Task A_Put_Built_On_A_Stale_Read_Answers_409_And_A_Current_One_Goes_Through()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var read = Guid.NewGuid();
        var department = await Create(client, read);
        var current = await OtherWriter(department.Id);

        var stale = await client.PutAsJsonAsync($"/departments/{department.Id}", Input("Stale edit", read, department.Id));
        var fresh = await client.PutAsJsonAsync($"/departments/{department.Id}", Input("Fresh edit", current, department.Id));

        Assert.Equal(HttpStatusCode.Conflict, stale.StatusCode);
        var problem = await stale.Content.ReadFromJsonAsync<ProblemDetails>();
        Assert.Equal("Concurrency conflict", problem!.Title);
        Assert.Equal(HttpStatusCode.OK, fresh.StatusCode);
        var saved = (await fresh.Content.ReadFromJsonAsync<SaveResult<DepartmentDto>>())!.Item;
        Assert.Equal("Fresh edit", saved.Title);
    }

    [Fact]
    public async Task A_Put_Without_A_Token_Is_Not_Checked()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var department = await Create(client, Guid.NewGuid());
        await OtherWriter(department.Id);

        var response = await client.PutAsJsonAsync($"/departments/{department.Id}", Input("Unchecked edit", Guid.Empty, department.Id));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var saved = (await response.Content.ReadFromJsonAsync<SaveResult<DepartmentDto>>())!.Item;
        Assert.NotEqual(Guid.Empty, saved.ConcurrencyToken);
    }

    [Fact]
    public async Task A_Patch_Is_Checked_Only_When_Its_Body_Carries_The_Token()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var read = Guid.NewGuid();
        var department = await Create(client, read);
        await OtherWriter(department.Id);
        var url = $"/departments/{department.Id}";

        // the merge base is the stored row, so a body without the token carries the current one
        var withoutToken = await client.PatchAsync(url, JsonContent.Create(new { title = "Patched" }));
        var staleToken = await client.PatchAsync(url, JsonContent.Create(new { title = "Stale patch", concurrencyToken = read }));

        Assert.Equal(HttpStatusCode.OK, withoutToken.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, staleToken.StatusCode);
    }

    [Fact]
    public async Task A_Delete_Carries_No_Token_And_Is_Not_Refused()
    {
        var app = new WebApplicationFactory<Program>();
        using var client = app.CreateClient();
        var department = await Create(client, Guid.NewGuid());
        await OtherWriter(department.Id);

        var response = await client.DeleteAsync($"/departments/{department.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }
}
