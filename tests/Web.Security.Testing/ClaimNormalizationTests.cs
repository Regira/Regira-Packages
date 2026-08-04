using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.DependencyInjection;
using Regira.Security.Authentication.Core.Models;
using Regira.Security.Authentication.Core.Services;
using Regira.Security.Authentication.Jwt.Extensions;
using Shouldly;
using System.Security.Claims;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// Normalization is asserted against hand-built principals rather than through a host, because the shapes that
/// matter come from providers no test can stand up: Entra's <c>roles</c>/<c>oid</c>, ASP.NET Identity's URI
/// spellings, the API-key handler's <see cref="ClaimTypes.Role"/>. A principal is the whole contract.
/// </summary>
public class ClaimNormalizationTests
{
    private const string AuthenticationType = "Test";

    /// <summary>
    /// ⚠️ The assertion the whole exercise exists for. These three checks disagree on an un-normalized principal —
    /// <c>[Authorize(Roles = …)]</c> resolves through the identity's own role type while
    /// <c>RequireClaim("role", …)</c> pins one spelling — so a policy that must accept two schemes could not be
    /// written without asserting over both. After normalization all three hold, whichever spelling the provider used.
    /// </summary>
    [Theory]
    [InlineData(JwtClaimTypesRole)]
    [InlineData("roles")]
    [InlineData(ClaimTypes.Role)]
    public async Task Test_Every_Role_Spelling_Satisfies_Every_Kind_Of_Check(string sourceRoleType)
    {
        var identity = ClaimsNormalizer.Normalize([new Claim(sourceRoleType, "admin")], AuthenticationType);
        var principal = new ClaimsPrincipal(identity);

        // 1. what [Authorize(Roles = "admin")] resolves
        principal.IsInRole("admin").ShouldBeTrue();
        // 2. what a hand-written claim check reads
        principal.HasClaim(RegiraClaimTypes.Role, "admin").ShouldBeTrue();
        // 3. an actual authorization policy
        (await EvaluateRequireClaimPolicy(principal, "admin")).Succeeded.ShouldBeTrue();
    }

    /// <summary>
    /// Nothing is renamed or dropped. Entra's <c>roles</c> in particular has to survive: a consumer reading it
    /// directly is entitled to keep working, which is why normalization adds rather than substitutes.
    /// </summary>
    [Fact]
    public void Test_Source_Claims_Survive_Untouched()
    {
        var identity = ClaimsNormalizer.Normalize(
        [
            new Claim("roles", "admin"),
            new Claim("oid", "entra-object-id"),
            new Claim("department", "engineering")
        ], AuthenticationType);

        identity.HasClaim("roles", "admin").ShouldBeTrue();
        identity.HasClaim("oid", "entra-object-id").ShouldBeTrue();
        // an unmapped claim passes through verbatim, same contract as ApiKeyOwner.Claims
        identity.HasClaim("department", "engineering").ShouldBeTrue();
    }

    [Fact]
    public void Test_Entra_Shape_Gains_The_Canonical_Claims()
    {
        var identity = ClaimsNormalizer.Normalize(
        [
            new Claim("oid", "entra-object-id"),
            new Claim("preferred_username", "someone@contoso.com"),
            new Claim("roles", "admin"),
            new Claim("roles", "editor")
        ], AuthenticationType);

        identity.FindFirst(RegiraClaimTypes.Subject)!.Value.ShouldBe("entra-object-id");
        identity.FindFirst(RegiraClaimTypes.Name)!.Value.ShouldBe("someone@contoso.com");
        identity.FindAll(RegiraClaimTypes.Role).Select(claim => claim.Value)
            .ShouldBe(["admin", "editor"], ignoreOrder: true);
    }

    [Fact]
    public void Test_ApiKey_Shape_Gains_The_Canonical_Claims()
    {
        var identity = ClaimsNormalizer.Normalize(
        [
            new Claim(ClaimTypes.NameIdentifier, "TestOwnerId"),
            new Claim(ClaimTypes.Role, "admin")
        ], AuthenticationType);

        identity.FindFirst(RegiraClaimTypes.Subject)!.Value.ShouldBe("TestOwnerId");
        identity.FindFirst(RegiraClaimTypes.Role)!.Value.ShouldBe("admin");
        // and the original spelling is still there
        identity.HasClaim(ClaimTypes.Role, "admin").ShouldBeTrue();
    }

    /// <summary>A provider already spelling a claim canonically is left exactly as it was — no duplicate.</summary>
    [Fact]
    public void Test_Canonical_Claim_Is_Not_Duplicated()
    {
        var identity = ClaimsNormalizer.Normalize(
        [
            new Claim(RegiraClaimTypes.Subject, "user-1"),
            new Claim(RegiraClaimTypes.Role, "admin")
        ], AuthenticationType);

        identity.FindAll(RegiraClaimTypes.Subject).Count().ShouldBe(1);
        identity.FindAll(RegiraClaimTypes.Role).Count().ShouldBe(1);
    }

    /// <summary>
    /// Two spellings of the same role collapse to one canonical claim. Without the de-duplication a principal
    /// carrying both would accumulate a copy per spelling.
    /// </summary>
    [Fact]
    public void Test_Same_Role_Under_Two_Spellings_Collapses()
    {
        var identity = ClaimsNormalizer.Normalize(
        [
            new Claim("roles", "admin"),
            new Claim(ClaimTypes.Role, "admin")
        ], AuthenticationType);

        identity.FindAll(RegiraClaimTypes.Role).Count().ShouldBe(1);
    }

    /// <summary>
    /// Normalizing an already-normalized principal changes nothing. It matters because a cookie principal is
    /// re-read on every request, so anything running per-request must not accumulate.
    /// </summary>
    [Fact]
    public void Test_Normalization_Is_Idempotent()
    {
        var once = ClaimsNormalizer.Normalize(
        [
            new Claim(ClaimTypes.NameIdentifier, "user-1"),
            new Claim(ClaimTypes.Role, "admin"),
            new Claim(ClaimTypes.Role, "editor")
        ], AuthenticationType);

        var twice = ClaimsNormalizer.Normalize(once.Claims, AuthenticationType);

        twice.Claims.Count().ShouldBe(once.Claims.Count());
    }

    /// <summary>The identity's own name and role types are what make the canonical claims resolve.</summary>
    [Fact]
    public void Test_Identity_Resolves_Through_The_Canonical_Types()
    {
        var identity = ClaimsNormalizer.Normalize([new Claim(ClaimTypes.Name, "TestUser")], AuthenticationType);

        identity.NameClaimType.ShouldBe(RegiraClaimTypes.Name);
        identity.RoleClaimType.ShouldBe(RegiraClaimTypes.Role);
        identity.Name.ShouldBe("TestUser");
    }

    /// <summary>A blank claim value must not win the search and mask a real value further down the list.</summary>
    [Fact]
    public void Test_Blank_Source_Value_Is_Skipped()
    {
        var identity = ClaimsNormalizer.Normalize(
        [
            new Claim(ClaimTypes.NameIdentifier, "   "),
            new Claim(JwtClaimTypesSubject, "user-1")
        ], AuthenticationType);

        identity.FindFirst(RegiraClaimTypes.Subject)!.Value.ShouldBe("user-1");
    }

    [Fact]
    public void Test_FindRoles_Reads_Every_Spelling()
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
        [
            new Claim(JwtClaimTypesRole, "a"),
            new Claim("roles", "b"),
            new Claim(ClaimTypes.Role, "c"),
            new Claim(ClaimTypes.Role, "a")
        ], AuthenticationType));

        principal.FindRoles().ShouldBe(["a", "b", "c"], ignoreOrder: true);
    }

    /// <summary>
    /// Scopes arrive as one space-delimited string, so a plain claim comparison fails against a token that grants
    /// the scope. Both spellings are covered — Entra uses <c>scp</c>, most others <c>scope</c>.
    /// </summary>
    [Theory]
    [InlineData("scp")]
    [InlineData("scope")]
    public void Test_HasScope_Splits_The_Delimited_Value(string scopeClaimType)
    {
        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            [new Claim(scopeClaimType, "openid profile api.read")], AuthenticationType));

        principal.HasScope("api.read").ShouldBeTrue();
        principal.HasScope("openid").ShouldBeTrue();
        principal.HasScope("api.write").ShouldBeFalse();
        // the naive check this helper exists to replace
        principal.HasClaim(scopeClaimType, "api.read").ShouldBeFalse();
    }

    // Literals rather than the Duende constants: [InlineData] needs compile-time constants, and spelling them out
    // here is what makes the test readable as "these are the three spellings in play".
    private const string JwtClaimTypesRole = "role";
    private const string JwtClaimTypesSubject = "sub";

    private static async Task<AuthorizationResult> EvaluateRequireClaimPolicy(ClaimsPrincipal principal, string role)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddAuthorization();

        await using var provider = services.BuildServiceProvider();
        var policy = new AuthorizationPolicyBuilder()
            .RequireClaim(RegiraClaimTypes.Role, role)
            .Build();

        return await provider.GetRequiredService<IAuthorizationService>().AuthorizeAsync(principal, null, policy);
    }
}
