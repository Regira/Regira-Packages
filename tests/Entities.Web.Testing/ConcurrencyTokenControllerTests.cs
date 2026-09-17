using Entities.Web.Testing.Infrastructure;
using System.Net;
using System.Net.Http.Json;
using Entities.TestApi.Infrastructure;
using Entities.TestApi.Infrastructure.Departments;
using Entities.TestApi.Infrastructure.Persons;
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
public class ConcurrencyTokenControllerTests : IClassFixture<ContosoApiFactory>, IDisposable
{
    private readonly ContosoContext _dbContext;
    private readonly ContosoApiFactory _factory;
    public ConcurrencyTokenControllerTests(ContosoApiFactory factory)
    {
        _factory = factory;
        _dbContext = factory.CreateDbContext();
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
        using var client = _factory.CreateClient();
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
        using var client = _factory.CreateClient();
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
        using var client = _factory.CreateClient();
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
        using var client = _factory.CreateClient();
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
        using var client = _factory.CreateClient();
        var department = await Create(client, Guid.NewGuid());
        await OtherWriter(department.Id);

        var response = await client.DeleteAsync($"/departments/{department.Id}");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    /// <summary>
    /// <c>Reservation</c> declares its token <c>[VersionStamp(Required = true)]</c> and is owned through
    /// <c>Person.Reservations</c>, so the check runs while the person's own <c>PUT</c> syncs the collection, and
    /// what it throws names <c>Reservation</c>, not <c>Person</c>. The generated actions catch their own entity's
    /// input exception only; this one reaches the client as 400 through the application-wide
    /// <c>EntityExceptionFilter</c> that <c>ConfigureDefaultJsonOptions()</c> registers. A host without the filter
    /// answers 500 here, as it does for any rule a related entity's write breaks.
    /// </summary>
    [Fact]
    public async Task A_Required_Stamp_Left_Out_Of_A_Child_Row_Is_A_400_Naming_The_Token()
    {
        using var client = _factory.CreateClient();
        // the insert is never refused: there is nothing read yet to prove
        var created = await client.PostAsJsonAsync("/persons", new PersonInputDto
        {
            GivenName = "Ada",
            LastName = "Lovelace",
            Reservations = [new ReservationInputDto { Room = "A1" }]
        });
        created.EnsureSuccessStatusCode();
        var person = (await created.Content.ReadFromJsonAsync<SaveResult<PersonDto>>())!.Item;
        var stored = await _dbContext.Reservations.AsNoTracking().SingleAsync(x => x.PersonId == person.Id);

        PersonInputDto Update(Guid token) => new()
        {
            Id = person.Id,
            GivenName = "Ada",
            LastName = "Lovelace",
            Reservations = [new ReservationInputDto { Id = stored.Id, Room = "B2", ConcurrencyToken = token }]
        };
        var withoutToken = await client.PutAsJsonAsync($"/persons/{person.Id}", Update(Guid.Empty));
        var withToken = await client.PutAsJsonAsync($"/persons/{person.Id}", Update(stored.ConcurrencyToken));

        Assert.NotEqual(Guid.Empty, stored.ConcurrencyToken);
        Assert.Equal(HttpStatusCode.BadRequest, withoutToken.StatusCode);
        // the same flat map ControllerExtensions.Save returns for the action's own entity (keys camelCased by
        // this host's Newtonsoft resolver)
        var errors = await withoutToken.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        Assert.Contains("concurrencyToken", errors!.Keys);
        Assert.Equal(HttpStatusCode.OK, withToken.StatusCode);
        var room = (await _dbContext.Reservations.AsNoTracking().SingleAsync(x => x.Id == stored.Id)).Room;
        Assert.Equal("B2", room);
    }
}
