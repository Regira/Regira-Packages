using Duende.IdentityModel.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Jwt.Services;
using Shouldly;
using System.Net;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text.Json;
using Web.Security.Testing.Infrastructure;
using Web.Security.Testing.Infrastructure.Identity;
using Xunit;

namespace Web.Security.Testing;

// Proves the Identity → JWT → [Authorize(Roles=…)] chain the roles-end-to-end recipe documents, spelling
// by spelling — the doc states nothing these tests do not show:
//  * AddIdentityCore alone emits NO role claims; .AddRoles<TRole>() swaps in the role-aware factory.
//  * That factory emits roles under IdentityOptions.ClaimsIdentity.RoleClaimType — ClaimTypes.Role
//    (the WS-2008 URI) unless configured.
//  * The outbound claim map renames only sub/name/email, so whatever spelling the factory used reaches
//    the raw JWT payload unrenamed — and the local JWT scheme validates with RoleClaimType "role" and no
//    claims normalizer, so URI-spelled roles do NOT satisfy [Authorize(Roles=…)].
//  * Setting ClaimsIdentity.RoleClaimType = "role" (RegiraClaimTypes.Role) at AddIdentityCore aligns the
//    whole chain: payload carries "role", the scheme reads "role", role gates admit and reject correctly.
public class RoleClaimsEndToEndTests : IClassFixture<TestingWebApplicationFactory<RolesStartup>>
{
    private readonly WebApplicationFactory<RolesStartup> _factory;

    public RoleClaimsEndToEndTests(TestingWebApplicationFactory<RolesStartup> factory)
    {
        _factory = factory
            .WithWebHostBuilder(builder => builder.UseSolutionRelativeContentRoot("tests"));
    }

    // --- what the Identity claims factory emits ---------------------------------------------------

    [Fact]
    public async Task Factory_WithoutAddRoles_EmitsNoRoleClaims()
    {
        var principal = await BuildPrincipal(addRoles: false, roleClaimType: null, dbName: "roles-none");

        principal.Claims.Where(c =>
                c.Type == ClaimTypes.Role || c.Type == "role" || c.Type == "roles")
            .ShouldBeEmpty("AddIdentityCore alone registers the role-less claims factory");
    }

    [Fact]
    public async Task Factory_WithAddRoles_EmitsRoles_UnderTheClaimTypesRoleUri()
    {
        var principal = await BuildPrincipal(addRoles: true, roleClaimType: null, dbName: "roles-default", "Manager");

        principal.FindAll(ClaimTypes.Role).Select(c => c.Value).ShouldContain("Manager");
        principal.FindAll("role").ShouldBeEmpty("the default RoleClaimType is the WS-2008 URI, not \"role\"");
    }

    [Fact]
    public async Task Factory_WithConfiguredRoleClaimType_EmitsRoles_UnderRole()
    {
        var principal = await BuildPrincipal(addRoles: true, roleClaimType: RegiraClaimTypes.Role, dbName: "roles-canonical", "Manager");

        principal.FindAll(RegiraClaimTypes.Role).Select(c => c.Value).ShouldContain("Manager");
        principal.FindAll(ClaimTypes.Role).ShouldBeEmpty();
    }

    // --- what reaches the raw JWT payload ---------------------------------------------------------

    [Fact]
    public void UriSpelledRoles_PassThrough_TheOutboundMap_Unrenamed()
    {
        // The outbound map AddJwtAuthentication installs renames only sub/name/email — a ClaimTypes.Role
        // claim keeps its full URI in the payload. This is why a raw-token consumer (the SPA) must probe
        // the URI spelling too.
        const string secret = "regira-web-security-testing-roles-signing-secret-key-0123456789-abcdefghijklmnop";
        new ServiceCollection().AddJwtAuthentication(o => o.Secret = secret);
        var helper = new JwtTokenHelper(new JwtTokenOptions { Secret = secret });

        var token = helper.Create([new Claim(ClaimTypes.Role, "Manager"), new Claim(ClaimTypes.Name, "someone")]);

        var payload = DecodePayload(token);
        payload.TryGetProperty(ClaimTypes.Role, out var role).ShouldBeTrue("the URI claim type must survive minting unrenamed");
        role.GetString().ShouldBe("Manager");
        payload.TryGetProperty("role", out _).ShouldBeFalse();
        payload.TryGetProperty("name", out _).ShouldBeTrue("ClaimTypes.Name is one of the three renamed claims");
    }

    [Fact]
    public async Task ConfiguredRoleClaimType_ReachesTheRawPayload_AsRole()
    {
        const string username = "payload@test.local";
        await SeedUser(username, "pass1", "Manager");
        var client = _factory.CreateClient();

        var token = await Authenticate(client, username, "pass1");

        var payload = DecodePayload(token);
        payload.TryGetProperty("role", out var role).ShouldBeTrue();
        RoleValues(role).ShouldContain("Manager");
        payload.TryGetProperty(ClaimTypes.Role, out _).ShouldBeFalse();
    }

    // --- what [Authorize(Roles=…)] does with it ---------------------------------------------------

    [Fact]
    public async Task RoleGate_Admits_A_RoleHolder()
    {
        const string username = "manager@test.local";
        await SeedUser(username, "pass1", "Manager");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, username, "pass1"));

        var response = await client.GetAsync("reports");

        response.StatusCode.ShouldBe(HttpStatusCode.OK);
    }

    [Fact]
    public async Task RoleGate_Rejects_An_Authenticated_User_Without_The_Role()
    {
        const string username = "employee@test.local";
        await SeedUser(username, "pass1");
        var client = _factory.CreateClient();
        client.SetBearerToken(await Authenticate(client, username, "pass1"));

        var response = await client.GetAsync("reports");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task RoleGate_Rejects_UriSpelledRoles_On_The_Local_Scheme()
    {
        // The local JWT scheme validates with RoleClaimType "role" and runs no claims normalizer
        // (normalization belongs to the external-bearer/OIDC paths) — a token carrying only the
        // WS-2008 URI spelling authenticates but does not satisfy the role gate. This is the failure
        // the ClaimsIdentity.RoleClaimType line in the recipe exists to prevent.
        var client = _factory.CreateClient();
        var helper = _factory.Services.GetRequiredService<Regira.Security.Authentication.Jwt.Abstraction.ITokenHelper>();
        var token = helper.Create([new Claim(ClaimTypes.Role, "Manager"), new Claim("sub", "uri-spelled")]);
        client.SetBearerToken(token);

        var response = await client.GetAsync("reports");

        response.StatusCode.ShouldBe(HttpStatusCode.Forbidden);
    }

    // --- helpers ----------------------------------------------------------------------------------

    private static async Task<ClaimsPrincipal> BuildPrincipal(bool addRoles, string? roleClaimType, string dbName, params string[] roles)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<TestDbContext>(o => o.UseInMemoryDatabase($"Web.Security.Testing.{dbName}"));
        var builder = services.AddIdentityCore<TestUser>(o =>
        {
            if (roleClaimType != null)
            {
                o.ClaimsIdentity.RoleClaimType = roleClaimType;
            }
        });
        if (addRoles)
        {
            builder = builder.AddRoles<IdentityRole>();
        }
        builder.AddEntityFrameworkStores<TestDbContext>();

        await using var sp = services.BuildServiceProvider();
        using var scope = sp.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        var user = new TestUser { UserName = $"{dbName}@test.local" };
        (await userManager.CreateAsync(user)).Succeeded.ShouldBeTrue();
        if (addRoles)
        {
            var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
            foreach (var role in roles)
            {
                (await roleManager.CreateAsync(new IdentityRole(role))).Succeeded.ShouldBeTrue();
                (await userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
            }
        }
        var factory = scope.ServiceProvider.GetRequiredService<IUserClaimsPrincipalFactory<TestUser>>();
        return await factory.CreateAsync(user);
    }

    private async Task SeedUser(string username, string password, params string[] roles)
    {
        using var scope = _factory.Services.CreateScope();
        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<TestUser>>();
        if (await userManager.FindByNameAsync(username) != null)
        {
            return;
        }
        var user = new TestUser { UserName = username, Email = username };
        (await userManager.CreateAsync(user, password)).Succeeded.ShouldBeTrue();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
        foreach (var role in roles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                (await roleManager.CreateAsync(new IdentityRole(role))).Succeeded.ShouldBeTrue();
            }
            (await userManager.AddToRoleAsync(user, role)).Succeeded.ShouldBeTrue();
        }
    }

    private static async Task<string> Authenticate(HttpClient client, string username, string password)
    {
        var response = await client.PostAsJsonAsync("auth?clientApp=test", new { username, password });
        response.StatusCode.ShouldBe(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<AuthResult>();
        return result!.Token!;
    }

    /// <summary>Raw base64url payload decode — no token handler, no inbound map, exactly what a SPA sees.</summary>
    private static JsonElement DecodePayload(string jwt)
    {
        var segment = jwt.Split('.')[1].Replace('-', '+').Replace('_', '/');
        segment = segment.PadRight(segment.Length + (4 - segment.Length % 4) % 4, '=');
        using var document = JsonDocument.Parse(Convert.FromBase64String(segment));
        return document.RootElement.Clone();
    }

    /// <summary>A JSON claim is a string for one value, an array for several.</summary>
    private static string[] RoleValues(JsonElement claim)
        => claim.ValueKind == JsonValueKind.Array
            ? claim.EnumerateArray().Select(e => e.GetString()!).ToArray()
            : [claim.GetString()!];

    private record AuthResult
    {
        public bool IsAuthenticated { get; init; }
        public string? Token { get; init; }
    }
}
