using Entities.Web.Testing.Infrastructure;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;

namespace Entities.Web.Testing;

/// <summary>
/// Over HTTP, against the real MVC pipeline: the unit tests prove <c>EntityExceptionFilter.OnException</c> and
/// that it lands in <c>MvcOptions.Filters</c>, but only a request proves MVC actually runs it — which is the
/// claim that matters, since the mapping used to reach the generated write actions alone.
/// </summary>
public class DomainActionExceptionTests(ContosoApiFactory factory) : IClassFixture<ContosoApiFactory>
{
    private HttpClient Client() => factory.CreateClient();

    [Fact]
    public async Task Input_Exception_From_A_Hand_Written_Action_Is_A_400_With_Its_Field_Errors()
    {
        using var client = Client();

        var response = await client.PostAsync("/domain-actions/input", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        // The flat SerializableError map BadRequest(ModelState) produces — the same body
        // ControllerExtensions.Save returns, with no ProblemDetails "errors" wrapper around it. The key reads
        // `title` only because this host serializes with Newtonsoft's camel-case resolver; System.Text.Json
        // leaves dictionary keys as thrown (`Title`).
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        Assert.Equal(["Only a draft course can be renamed."], errors!["title"]);
    }

    [Fact]
    public async Task An_Exception_Naming_A_Related_Entity_Is_Mapped_Too()
    {
        using var client = Client();

        var response = await client.PostAsync("/domain-actions/input-related", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        Assert.Equal(["Unknown department."], errors!["departmentId"]);
    }

    [Fact]
    public async Task An_Input_Exception_Without_Field_Errors_Carries_Its_Message()
    {
        using var client = Client();

        var response = await client.PostAsync("/domain-actions/input-without-field-errors", null);

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var errors = await response.Content.ReadFromJsonAsync<Dictionary<string, string[]>>();
        Assert.Equal(["Credits must be positive."], errors![""]);
    }

    [Fact]
    public async Task Constraint_Exception_From_A_Hand_Written_Action_Is_A_409_Problem()
    {
        using var client = Client();

        var response = await client.PostAsync("/domain-actions/constraint", null);

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
        var problem = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        // The generic client message — the provider's text (which can leak index names) stays server-side.
        Assert.Equal("A database constraint rejected the change.", problem!.Detail);
        Assert.DoesNotContain("Courses.Title", JsonSerializer.Serialize(problem));
    }
}
