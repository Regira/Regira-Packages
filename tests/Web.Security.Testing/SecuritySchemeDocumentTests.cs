using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Security.Authentication.Cookie.Models;
using Shouldly;
using System.Text.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Composition;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// One document transformer describing every registered scheme, from the descriptors each contributed at
/// registration — instead of a transformer class per scheme.
/// </summary>
public class SecuritySchemeDocumentTests : IClassFixture<TestingWebApplicationFactory<AllSchemesStartup>>
{
    private readonly WebApplicationFactory<AllSchemesStartup> _factory;

    public SecuritySchemeDocumentTests(TestingWebApplicationFactory<AllSchemesStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    [Fact]
    public async Task Test_Bearer_Is_Declared_As_An_Http_Scheme()
    {
        var scheme = (await Schemes()).GetProperty("Bearer");

        scheme.GetProperty("type").GetString().ShouldBe("http");
        scheme.GetProperty("scheme").GetString().ShouldBe("bearer");
    }

    [Fact]
    public async Task Test_ApiKey_Is_Declared_With_Its_Header_Name()
    {
        var scheme = (await Schemes()).GetProperty(ApiKeyDefaults.AuthenticationScheme);

        scheme.GetProperty("type").GetString().ShouldBe("apiKey");
        scheme.GetProperty("in").GetString().ShouldBe("header");
        scheme.GetProperty("name").GetString().ShouldBe(ApiKeyDefaults.HeaderName);
    }

    /// <summary>
    /// OpenAPI has no cookie security type, so the accepted convention is an API key located in the cookie. Getting
    /// this shape right is the whole reason cookie needed a descriptor rather than a copy of the bearer transformer.
    /// </summary>
    [Fact]
    public async Task Test_Cookie_Is_Declared_As_An_ApiKey_In_The_Cookie()
    {
        var scheme = (await Schemes()).GetProperty(CookieAuthenticationDefaults.AuthenticationScheme);

        scheme.GetProperty("type").GetString().ShouldBe("apiKey");
        scheme.GetProperty("in").GetString().ShouldBe("cookie");
        scheme.GetProperty("name").GetString().ShouldBe(CookieAuthDefaults.CookieName);
    }

    [Fact]
    public async Task Test_OpenIdConnect_Is_Declared_With_Its_Discovery_Url()
    {
        var scheme = (await Schemes()).GetProperty(AllSchemesStartup.OidcScheme);

        scheme.GetProperty("type").GetString().ShouldBe("openIdConnect");
        scheme.GetProperty("openIdConnectUrl").GetString()
            .ShouldEndWith("/.well-known/openid-configuration");
    }

    /// <summary>
    /// The policy scheme forwards rather than authenticating and contributes no descriptor, so it must not appear —
    /// and every operation requirement must resolve to something that does.
    /// </summary>
    [Fact]
    public async Task Test_Policy_Scheme_Is_Not_Declared_And_Requirements_All_Resolve()
    {
        var document = await Document();
        var declared = document.RootElement
            .GetProperty("components").GetProperty("securitySchemes")
            .EnumerateObject().Select(scheme => scheme.Name).ToArray();

        declared.ShouldNotContain("Smart");

        var required = document.RootElement
            .GetProperty("paths").GetProperty("/protected").GetProperty("get").GetProperty("security")
            .EnumerateArray()
            .SelectMany(requirement => requirement.EnumerateObject().Select(scheme => scheme.Name))
            .ToArray();

        required.ShouldNotBeEmpty();
        required.ShouldAllBe(scheme => declared.Contains(scheme));
    }

    private async Task<JsonElement> Schemes()
        => (await Document()).RootElement.GetProperty("components").GetProperty("securitySchemes");

    private async Task<JsonDocument> Document()
    {
        var json = await _factory.CreateClient().GetStringAsync("openapi/v1.json");
        return JsonDocument.Parse(json);
    }
}
