using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Security.Authentication.Core.Models;
using Shouldly;
using System.Net;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.ApiKey;
using Web.Security.Testing.Infrastructure.Composition;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// ⚠️ The selector takes over <c>DefaultChallengeScheme</c>, which decides what an unauthenticated caller gets back.
/// Its forwarding rules are keyed on the credential a request <em>carries</em> — and a browser arriving at a guarded
/// page carries none, so without special handling the challenge falls through to the lowest-ordered rule (bearer) and
/// answers <c>401</c>. In an app whose whole point is interactive sign-in that makes login unreachable, and nothing
/// errors.
/// </summary>
public class SchemeSelectorChallengeTests : IClassFixture<TestingWebApplicationFactory<AllSchemesStartup>>
{
    private readonly WebApplicationFactory<AllSchemesStartup> _factory;

    public SchemeSelectorChallengeTests(TestingWebApplicationFactory<AllSchemesStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    /// <summary>
    /// A credential-less request to a guarded endpoint must start the sign-in flow, because this host registered an
    /// interactive sign-in scheme.
    /// </summary>
    [Fact]
    public async Task Test_Credential_Less_Request_Challenges_Through_The_Sign_In_Scheme()
    {
        var response = await CreateClient().GetAsync("protected");

        response.StatusCode.ShouldBe(HttpStatusCode.Found);
        response.Headers.Location!.ToString().ShouldStartWith("https://login.microsoftonline.com/fake/oauth2/v2.0/authorize");
    }

    /// <summary>The selector still owns authentication — a credential is read by the scheme that understands it.</summary>
    [Fact]
    public async Task Test_Credentials_Still_Route_By_Their_Shape()
    {
        var withKey = CreateClient();
        withKey.DefaultRequestHeaders.Add(ApiKeyDefaults.HeaderName, ApiKeyOwners.Default.Key);

        var response = await withKey.GetAsync("protected/me");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        (await response.Content.ReadAsStringAsync()).ShouldBe(ApiKeyOwners.Default.OwnerId);
    }

    /// <summary>Authentication stays with the policy scheme; only the challenge is delegated.</summary>
    [Fact]
    public async Task Test_Selector_Still_Owns_Authentication()
    {
        using var scope = _factory.Services.CreateScope();
        var schemes = scope.ServiceProvider.GetRequiredService<IAuthenticationSchemeProvider>();

        (await schemes.GetDefaultAuthenticateSchemeAsync())!.Name
            .ShouldBe(SchemeSelectorDefaults.AuthenticationScheme);
    }

    private HttpClient CreateClient() => _factory.CreateClient(new WebApplicationFactoryClientOptions
    {
        AllowAutoRedirect = false,
        BaseAddress = new Uri("https://localhost")
    });
}
