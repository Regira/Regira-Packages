using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Jwt.Models;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Bearer;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// Validating tokens an external authority signed — the path <c>AddJwtAuthentication</c> cannot serve at all,
/// because it derives a symmetric key from a secret and Entra signs with rotating asymmetric keys.
/// </summary>
public class EntraIdBearerTests : IClassFixture<TestingWebApplicationFactory<EntraBearerStartup>>
{
    private readonly WebApplicationFactory<EntraBearerStartup> _factory;

    public EntraIdBearerTests(TestingWebApplicationFactory<EntraBearerStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    [Fact]
    public async Task Test_Valid_Rs256_Token_Is_Accepted()
    {
        var response = await Get("protected", FakeAuthority.CreateToken());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_No_Token_Is_Unauthorized()
    {
        (await _factory.CreateClient().GetAsync("protected")).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>
    /// ⚠️ The headline result. Entra emits app roles as <c>roles</c> (plural) — <c>role</c> singular matches
    /// nothing — so without the role claim type and normalization this is a 403 against a token that visibly
    /// contains the role. It is the single most common Entra integration failure.
    /// </summary>
    [Fact]
    public async Task Test_Entra_App_Role_Satisfies_Authorize_Roles()
    {
        var response = await Get("protected/admin", FakeAuthority.CreateToken());

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_Token_Without_The_Role_Is_Forbidden()
    {
        var response = await Get("protected/admin", FakeAuthority.CreateToken(withRole: false));

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    /// <summary>
    /// Entra's own spellings survive and the canonical copies are added beside them — the additive contract. A
    /// consumer already reading <c>roles</c> or <c>oid</c> keeps working.
    /// </summary>
    [Fact]
    public async Task Test_Principal_Carries_Both_Entra_And_Canonical_Claims()
    {
        var report = await GetReport(FakeAuthority.CreateToken());

        report.ObjectId.ShouldBe(FakeAuthority.ObjectId);
        report.Roles.ShouldBe(FakeAuthority.AdminRole);

        report.Role.ShouldBe(FakeAuthority.AdminRole);
        report.IsInAdminRole.ShouldBeTrue();
        report.AllRoles.ShouldBe([FakeAuthority.AdminRole]);
        report.FoundUserName.ShouldBe(FakeAuthority.UserName);
    }

    /// <summary>
    /// <c>oid</c>, not <c>sub</c>, is the stable per-tenant user id: Entra's <c>sub</c> is pairwise per application,
    /// so two apps see different values for the same person. The canonical <c>sub</c> is already taken by the
    /// token's own pairwise value here, which is exactly why the guide says to key rows on <c>oid</c>.
    /// </summary>
    [Fact]
    public async Task Test_Pairwise_Subject_Is_Left_Alone()
    {
        var report = await GetReport(FakeAuthority.CreateToken());

        report.Subject.ShouldBe("PAIRWISE_SUBJECT_DO_NOT_KEY_ON_THIS");
        report.ObjectId.ShouldBe(FakeAuthority.ObjectId);
    }

    /// <summary>Scopes arrive as one space-delimited string, so this is the only correct way to read them.</summary>
    [Fact]
    public async Task Test_Delimited_Scope_Claim_Is_Readable()
    {
        var report = await GetReport(FakeAuthority.CreateToken());

        report.HasReadScope.ShouldBeTrue();
        report.HasWriteScope.ShouldBeFalse();
    }

    /// <summary>
    /// Both audience forms are accepted, because Entra issues either depending on how the client requested the
    /// scope. Pinning one form rejects half of otherwise valid callers.
    /// </summary>
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Test_Both_Audience_Forms_Are_Accepted(bool scoped)
    {
        var audience = scoped ? FakeAuthority.ScopedAudience : FakeAuthority.ClientId;

        var response = await Get("protected", FakeAuthority.CreateToken(audience: audience));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// A registration left at <c>accessTokenAcceptedVersion: null</c> issues v1 tokens under
    /// <c>sts.windows.net</c>. Accepting both spellings is what stops that presenting as <c>IDX10205</c>, an error
    /// naming neither the version nor the setting that caused it.
    /// </summary>
    [Fact]
    public async Task Test_V1_Issuer_Spelling_Is_Accepted()
    {
        var response = await Get("protected", FakeAuthority.CreateToken(issuer: FakeAuthority.V1Issuer()));

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Test_Unknown_Issuer_Is_Rejected()
    {
        var token = FakeAuthority.CreateToken(issuer: "https://login.microsoftonline.com/evil-tenant/v2.0");

        (await Get("protected", token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Test_Wrong_Audience_Is_Rejected()
    {
        var token = FakeAuthority.CreateToken(audience: "api://some-other-app");

        (await Get("protected", token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Test_Expired_Token_Is_Rejected()
    {
        var token = FakeAuthority.CreateToken(lifetime: TimeSpan.FromSeconds(-30));

        (await Get("protected", token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private async Task<EntraClaimsReport> GetReport(string token)
    {
        var response = await Get("entra/claims", token);
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        return (await response.Content.ReadFromJsonAsync<EntraClaimsReport>())!;
    }

    private Task<HttpResponseMessage> Get(string path, string token)
    {
        var httpClient = _factory.CreateClient();
        httpClient.SetBearerToken(token);
        return httpClient.GetAsync(path);
    }
}

/// <summary>
/// A wildcard tenant has no fixed issuer, so the issuer is tied to the token's own <c>tid</c>. Without that check a
/// multi-tenant API accepts tokens from any directory — and the failure is silent, because such a token is
/// perfectly valid, just not from a tenant this application ever agreed to trust.
/// </summary>
public class EntraMultiTenantBearerTests : IClassFixture<TestingWebApplicationFactory<EntraMultiTenantStartup>>
{
    private readonly WebApplicationFactory<EntraMultiTenantStartup> _factory;

    public EntraMultiTenantBearerTests(TestingWebApplicationFactory<EntraMultiTenantStartup> factory)
    {
        _factory = factory.WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    /// <summary>Any tenant is welcome, as long as the issuer is that tenant's own endpoint.</summary>
    [Theory]
    [InlineData(FakeAuthority.TenantId)]
    [InlineData(FakeAuthority.OtherTenantId)]
    public async Task Test_Issuer_Matching_The_Tokens_Tenant_Is_Accepted(string tenantId)
    {
        var token = FakeAuthority.CreateToken(issuer: FakeAuthority.V2Issuer(tenantId), tenantId: tenantId);

        (await Get(token)).StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    /// <summary>
    /// ⚠️ The security assertion. A token claiming one tenant but issued under another's endpoint must be rejected;
    /// accepting it would let anyone who can get a token from any directory in.
    /// </summary>
    [Fact]
    public async Task Test_Issuer_Not_Matching_The_Tokens_Tenant_Is_Rejected()
    {
        var token = FakeAuthority.CreateToken(
            issuer: FakeAuthority.V2Issuer(FakeAuthority.OtherTenantId),
            tenantId: FakeAuthority.TenantId);

        (await Get(token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    /// <summary>An issuer on a host that is not this instance is rejected however plausible it looks.</summary>
    [Fact]
    public async Task Test_Issuer_On_A_Foreign_Host_Is_Rejected()
    {
        var token = FakeAuthority.CreateToken(issuer: $"https://login.microsoftonline.com.evil.test/{FakeAuthority.TenantId}/v2.0");

        (await Get(token)).StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    private Task<HttpResponseMessage> Get(string token)
    {
        var httpClient = _factory.CreateClient();
        httpClient.SetBearerToken(token);
        return httpClient.GetAsync("protected");
    }
}

/// <summary>
/// Option shaping, asserted without a host. This is what covers the values a live host would fetch metadata from —
/// the authority string, the audiences, the issuer list — since the metadata request itself cannot be made in-process.
/// </summary>
public class EntraIdOptionsTests
{
    [Fact]
    public void Test_V2_Authority_Is_Derived_From_Tenant()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
        });

        options.Authority.ShouldBe($"https://login.microsoftonline.com/{FakeAuthority.TenantId}/v2.0");
    }

    [Fact]
    public void Test_V1_Authority_Omits_The_Version_Segment()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.UseV2Endpoint = false;
        });

        options.Authority.ShouldBe($"https://login.microsoftonline.com/{FakeAuthority.TenantId}");
    }

    [Fact]
    public void Test_Sovereign_Instance_Is_Honoured()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.Instance = "https://login.microsoftonline.us/";
        });

        options.Authority.ShouldBe($"https://login.microsoftonline.us/{FakeAuthority.TenantId}/v2.0");
    }

    [Fact]
    public void Test_Both_Audience_Forms_Are_Configured()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
        });

        options.Audiences.ShouldBe([$"api://{FakeAuthority.ClientId}", FakeAuthority.ClientId], ignoreOrder: true);
    }

    /// <summary>Entra's role claim type, not the canonical one — the plural is the whole point.</summary>
    [Fact]
    public void Test_Role_Claim_Type_Is_Plural()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
        });

        options.RoleClaimType.ShouldBe("roles");
    }

    [Fact]
    public void Test_Single_Tenant_Accepts_Both_Issuer_Spellings()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
        });

        options.ValidIssuers.ShouldBe(
        [
            $"https://login.microsoftonline.com/{FakeAuthority.TenantId}/v2.0",
            $"https://sts.windows.net/{FakeAuthority.TenantId}/"
        ], ignoreOrder: true);
    }

    /// <summary>A wildcard tenant gets no fixed issuer list — there is no single issuer to name.</summary>
    [Theory]
    [InlineData("common")]
    [InlineData("organizations")]
    public void Test_Multi_Tenant_Has_No_Fixed_Issuer_List(string tenantId)
    {
        var options = Expand(o =>
        {
            o.TenantId = tenantId;
            o.ClientId = FakeAuthority.ClientId;
        });

        options.ValidIssuers.ShouldBeNull();
    }

    /// <summary>
    /// ⚠️ Every customised claim list reaches the bearer options, not just roles. A half-copied set accepts the
    /// caller's configuration and ignores most of it — the caller sees a principal missing the identity they
    /// configured, with nothing to indicate why.
    /// </summary>
    [Fact]
    public void Test_All_Customised_Claim_Lists_Are_Carried_Over()
    {
        var options = Expand(o =>
        {
            o.TenantId = FakeAuthority.TenantId;
            o.ClientId = FakeAuthority.ClientId;
            o.Claims.SubjectClaimTypes.Insert(0, "my_sub");
            o.Claims.NameClaimTypes.Insert(0, "my_name");
            o.Claims.EmailClaimTypes.Insert(0, "my_email");
            o.Claims.RoleClaimTypes.Insert(0, "my_role");
        });

        // First, so it wins the "first non-empty match" search the caller configured it to win.
        options.Claims.SubjectClaimTypes[0].ShouldBe("my_sub");
        options.Claims.NameClaimTypes[0].ShouldBe("my_name");
        options.Claims.EmailClaimTypes[0].ShouldBe("my_email");
        options.Claims.RoleClaimTypes[0].ShouldBe("my_role");

        // and the defaults are still there behind them
        options.Claims.RoleClaimTypes.ShouldContain("roles");
        options.Claims.SubjectClaimTypes.ShouldContain("oid");
    }

    /// <summary>
    /// ⚠️ The consumer's escape hatch must not be able to drop the multi-tenant issuer check. The validator is chained
    /// after whatever <c>Configure</c> leaves behind, so a delegate that replaces <c>TokenValidationParameters</c>
    /// outright still ends up with it — a security control customization can overwrite is one that eventually is.
    /// </summary>
    [Fact]
    public void Test_Consumer_Configure_Cannot_Drop_The_Multi_Tenant_Issuer_Validator()
    {
        var options = Expand(o =>
        {
            o.TenantId = EntraIdDefaults.OrganizationsTenant;
            o.ClientId = FakeAuthority.ClientId;
            o.Configure = bearer => bearer.TokenValidationParameters = new TokenValidationParameters();
        });

        var handler = new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions();
        BearerAuthenticationExtensions.Apply(handler, options);

        handler.TokenValidationParameters.IssuerValidator.ShouldNotBeNull();
        handler.TokenValidationParameters.ValidateIssuer.ShouldBeTrue();
    }

    [Theory]
    [InlineData("", FakeAuthority.ClientId, nameof(EntraIdOptions.TenantId))]
    [InlineData(FakeAuthority.TenantId, "", nameof(EntraIdOptions.ClientId))]
    public void Test_Missing_Registration_Value_Throws_Naming_It(string tenantId, string clientId, string expected)
    {
        var exception = Should.Throw<InvalidOperationException>(() => Expand(o =>
        {
            o.TenantId = tenantId;
            o.ClientId = clientId;
        }));

        exception.Message.ShouldContain(expected);
    }

    /// <summary>
    /// Exactly one source of signing keys. Both set, or neither, is a configuration mistake worth failing at
    /// startup rather than on the first request.
    /// </summary>
    [Fact]
    public void Test_Bearer_Options_Require_Exactly_One_Key_Source()
    {
        Should.Throw<InvalidOperationException>(() => ApplyBearer(_ => { }))
            .Message.ShouldContain("Neither is set");

        Should.Throw<InvalidOperationException>(() => ApplyBearer(o =>
        {
            o.Authority = "https://issuer.example";
            o.Secret = new string('k', 64);
        })).Message.ShouldContain("Both are set");

        Should.NotThrow(() => ApplyBearer(o => o.Authority = "https://issuer.example"));
        Should.NotThrow(() => ApplyBearer(o => o.Secret = new string('k', 64)));
    }

    private static BearerValidationOptions Expand(Action<EntraIdOptions> configure)
    {
        var entra = new EntraIdOptions();
        configure(entra);

        var bearer = new BearerValidationOptions();
        EntraIdAuthenticationExtensions.ToBearerOptions(entra, bearer);
        return bearer;
    }

    private static void ApplyBearer(Action<BearerValidationOptions> configure)
    {
        var options = new BearerValidationOptions();
        configure(options);
        BearerAuthenticationExtensions.Apply(new Microsoft.AspNetCore.Authentication.JwtBearer.JwtBearerOptions(), options);
    }
}
