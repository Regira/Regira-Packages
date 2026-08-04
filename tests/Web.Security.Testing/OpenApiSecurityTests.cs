using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Shouldly;
using System.Text.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Jwt;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// A declared security scheme only makes the auth prompt appear; it says nothing about which endpoints need a
/// token. These assert the document actually describes the API's authorization, so a generated client — or a
/// reader of <c>/scalar</c> — can tell a public endpoint from a guarded one.
/// </summary>
public class OpenApiSecurityTests : IClassFixture<TestingWebApplicationFactory<JwtStartup>>
{
    private readonly WebApplicationFactory<JwtStartup> _factory;
    public OpenApiSecurityTests(TestingWebApplicationFactory<JwtStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    [Fact]
    public async Task Test_Document_Declares_The_Bearer_Scheme()
    {
        var document = await GetDocument();

        document.RootElement
            .GetProperty("components").GetProperty("securitySchemes")
            .TryGetProperty("Bearer", out _).ShouldBeTrue();
    }

    [Fact]
    public async Task Test_Guarded_Operation_Requires_The_Bearer_Scheme()
    {
        var document = await GetDocument();

        var security = GetOperation(document, "/protected", "get").GetProperty("security");
        security.EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name))
            .ShouldContain("Bearer");
    }

    [Fact]
    public async Task Test_Anonymous_Operation_Carries_No_Requirement()
    {
        var document = await GetDocument();

        // RequireAuthorization() puts authorization metadata on every controller endpoint, so an
        // [AllowAnonymous] action is the case that separates reading the metadata from assuming it.
        GetOperation(document, "/", "get").TryGetProperty("security", out _).ShouldBeFalse();
    }

    /// <summary>
    /// Minimal APIs reach the transformer through the other branch: no <c>ActionDescriptor</c> sits in the
    /// endpoint metadata, so the endpoint index misses and the action descriptor's own metadata is read — which
    /// for a minimal endpoint is the endpoint's, the case a controller does not exercise.
    /// </summary>
    [Fact]
    public async Task Test_Guarded_Minimal_Endpoint_Requires_The_Bearer_Scheme()
    {
        var document = await GetDocument();

        var security = GetOperation(document, "/minimal/protected", "get").GetProperty("security");
        security.EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name))
            .ShouldContain("Bearer");
    }

    [Fact]
    public async Task Test_Anonymous_Minimal_Endpoint_Carries_No_Requirement()
    {
        var document = await GetDocument();

        GetOperation(document, "/minimal/public", "get").TryGetProperty("security", out _).ShouldBeFalse();
    }

    private static JsonElement GetOperation(JsonDocument document, string path, string method)
        => document.RootElement.GetProperty("paths").GetProperty(path).GetProperty(method);

    private async Task<JsonDocument> GetDocument()
    {
        var httpClient = _factory.CreateClient();
        var json = await httpClient.GetStringAsync("openapi/v1.json");
        return JsonDocument.Parse(json);
    }
}
