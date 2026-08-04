using Regira.Security.Authentication.Jwt.Abstraction;
using Regira.Security.Authentication.Jwt.Models;
using Regira.Security.Authentication.Jwt.Services;
using Shouldly;
using System.Security.Claims;
using Xunit;

namespace Web.Security.Testing;

/// <summary>
/// Rotation and replay detection, asserted against the service directly — the security properties live there, not in
/// the HTTP surface.
/// </summary>
public class RefreshTokenTests
{
    private const string UserId = "USER_1";
    private const string Secret = "refresh-token-tests-secret-long-enough-for-hs512-0123456789012345";

    [Fact]
    public async Task Test_Issue_Returns_An_Access_And_A_Refresh_Token()
    {
        var (service, _) = Create();

        var pair = await service.Issue(UserId, Claims());

        pair.AccessToken.ShouldNotBeNullOrWhiteSpace();
        pair.RefreshToken.ShouldNotBeNullOrWhiteSpace();
        pair.AccessTokenExpiresAt.ShouldBeGreaterThan(DateTimeOffset.UtcNow);
        pair.RefreshTokenExpiresAt!.Value.ShouldBeGreaterThan(pair.AccessTokenExpiresAt);
    }

    [Fact]
    public async Task Test_Refresh_Issues_A_New_Pair()
    {
        var (service, _) = Create();
        var issued = await service.Issue(UserId, Claims());

        var refreshed = await service.Refresh(issued.RefreshToken!, Resolver());

        refreshed.ShouldNotBeNull();
        refreshed.RefreshToken.ShouldNotBe(issued.RefreshToken);
        refreshed.AccessToken.ShouldNotBeNullOrWhiteSpace();
    }

    /// <summary>Rotation: the token that was presented stops working the moment it is exchanged.</summary>
    [Fact]
    public async Task Test_Rotated_Token_Stops_Working()
    {
        var (service, _) = Create();
        var issued = await service.Issue(UserId, Claims());
        await service.Refresh(issued.RefreshToken!, Resolver());

        (await service.Refresh(issued.RefreshToken!, Resolver())).ShouldBeNull();
    }

    /// <summary>
    /// ⚠️ The point of rotating at all. A rotated token should never be presented again, so a replay means two parties
    /// hold it — and from the server there is no way to tell which one is asking. The whole chain ends, which
    /// invalidates the *attacker's* freshly-minted token as well as the victim's.
    /// </summary>
    [Fact]
    public async Task Test_Replaying_A_Rotated_Token_Ends_The_Whole_Chain()
    {
        var (service, _) = Create();
        var first = await service.Issue(UserId, Claims());
        var second = await service.Refresh(first.RefreshToken!, Resolver());

        // The stolen copy of the already-used token is replayed.
        (await service.Refresh(first.RefreshToken!, Resolver())).ShouldBeNull();

        // …and the legitimate client's current token is dead too, because the session is no longer trustworthy.
        (await service.Refresh(second!.RefreshToken!, Resolver())).ShouldBeNull();
    }

    /// <summary>With reuse detection off, a replay is refused but the rest of the chain survives.</summary>
    [Fact]
    public async Task Test_Reuse_Detection_Can_Be_Turned_Off()
    {
        var (service, _) = Create(o => o.RevokeFamilyOnReuse = false);
        var first = await service.Issue(UserId, Claims());
        var second = await service.Refresh(first.RefreshToken!, Resolver());

        (await service.Refresh(first.RefreshToken!, Resolver())).ShouldBeNull();
        (await service.Refresh(second!.RefreshToken!, Resolver())).ShouldNotBeNull();
    }

    /// <summary>
    /// ⚠️ Claims are re-read on every refresh. Replaying the set captured at sign-in would keep a role that was removed
    /// an hour ago in force for the life of the refresh token.
    /// </summary>
    [Fact]
    public async Task Test_Refresh_Reads_The_Users_Current_Claims()
    {
        var (service, _) = Create();
        var issued = await service.Issue(UserId, Claims("admin"));

        var refreshed = await service.Refresh(issued.RefreshToken!, _ => Task.FromResult<IEnumerable<Claim>?>(Claims("reader")));

        refreshed.ShouldNotBeNull();
        ReadRoles(refreshed.AccessToken).ShouldBe(["reader"]);
    }

    /// <summary>A resolver returning null — user gone, locked out, disabled — refuses the refresh and ends the chain.</summary>
    [Fact]
    public async Task Test_Unresolvable_User_Refuses_And_Revokes()
    {
        var (service, _) = Create();
        var first = await service.Issue(UserId, Claims());
        var second = await service.Refresh(first.RefreshToken!, Resolver());

        (await service.Refresh(second!.RefreshToken!, _ => Task.FromResult<IEnumerable<Claim>?>(null))).ShouldBeNull();

        // The refusal revoked the family, so nothing from this session works again.
        (await service.Refresh(second.RefreshToken!, Resolver())).ShouldBeNull();
    }

    [Fact]
    public async Task Test_Unknown_Token_Is_Refused()
    {
        var (service, _) = Create();

        (await service.Refresh("not-a-token", Resolver())).ShouldBeNull();
        (await service.Refresh(string.Empty, Resolver())).ShouldBeNull();
    }

    [Fact]
    public async Task Test_Expired_Token_Is_Refused()
    {
        var (service, _) = Create(o => o.LifeSpan = -1);
        var issued = await service.Issue(UserId, Claims());

        (await service.Refresh(issued.RefreshToken!, Resolver())).ShouldBeNull();
    }

    /// <summary>
    /// The absolute ceiling caps the whole chain. Without it a token that is rotated often enough never expires and a
    /// session lives forever.
    /// </summary>
    [Fact]
    public async Task Test_Family_Ceiling_Caps_The_Rotation_Chain()
    {
        var (service, _) = Create(o =>
        {
            o.LifeSpan = 60 * 60 * 24 * 14;
            o.AbsoluteLifeSpan = 1;
        });

        var issued = await service.Issue(UserId, Claims());

        // Each token's own expiry is clamped to the family's, so it cannot outlive the chain.
        issued.RefreshTokenExpiresAt!.Value.ShouldBeLessThan(DateTimeOffset.UtcNow.AddMinutes(1));
    }

    [Fact]
    public async Task Test_Explicit_Revoke_Ends_The_Session()
    {
        var (service, _) = Create();
        var issued = await service.Issue(UserId, Claims());

        await service.Revoke(issued.RefreshToken!);

        (await service.Refresh(issued.RefreshToken!, Resolver())).ShouldBeNull();
    }

    [Fact]
    public async Task Test_Revoke_All_For_User_Ends_Every_Session()
    {
        var (service, _) = Create();
        var laptop = await service.Issue(UserId, Claims());
        var phone = await service.Issue(UserId, Claims());
        var somebodyElse = await service.Issue("USER_2", Claims());

        await service.RevokeAllForUser(UserId);

        (await service.Refresh(laptop.RefreshToken!, Resolver())).ShouldBeNull();
        (await service.Refresh(phone.RefreshToken!, Resolver())).ShouldBeNull();
        (await service.Refresh(somebodyElse.RefreshToken!, Resolver())).ShouldNotBeNull();
    }

    /// <summary>
    /// The token is not recoverable from the store. A leaked database must not hand over usable sessions, so what is
    /// persisted is a hash — and the raw token appears nowhere in the record.
    /// </summary>
    [Fact]
    public async Task Test_Store_Holds_A_Hash_Not_The_Token()
    {
        var (service, store) = Create();
        var issued = await service.Issue(UserId, Claims());

        (await store.Find(issued.RefreshToken!)).ShouldBeNull();

        // Deterministic and unsalted by design — the store is looked up by it.
        var key = Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(
            System.Text.Encoding.UTF8.GetBytes(issued.RefreshToken!))).ToLowerInvariant();
        var record = await store.Find(key);
        record.ShouldNotBeNull();
        record.UserId.ShouldBe(UserId);
    }

    [Fact]
    public async Task Test_Hashing_Can_Be_Turned_Off()
    {
        var (service, store) = Create(o => o.HashStoredTokens = false);
        var issued = await service.Issue(UserId, Claims());

        (await store.Find(issued.RefreshToken!)).ShouldNotBeNull();
    }

    /// <summary>Without rotation the same token keeps working, and the access token is still renewed.</summary>
    [Fact]
    public async Task Test_Rotation_Can_Be_Turned_Off()
    {
        var (service, _) = Create(o => o.Rotate = false);
        var issued = await service.Issue(UserId, Claims());

        var refreshed = await service.Refresh(issued.RefreshToken!, Resolver());

        refreshed!.RefreshToken.ShouldBe(issued.RefreshToken);
        (await service.Refresh(issued.RefreshToken!, Resolver())).ShouldNotBeNull();
    }

    /// <summary>Generated tokens are unique and carry the configured entropy.</summary>
    [Fact]
    public async Task Test_Tokens_Are_Unique_And_Random()
    {
        var (service, _) = Create();

        var tokens = new HashSet<string>();
        for (var i = 0; i < 50; i++)
        {
            tokens.Add((await service.Issue(UserId, Claims())).RefreshToken!);
        }

        tokens.Count.ShouldBe(50);
    }

    /// <summary>
    /// ⚠️ Rotation must be atomic. Two concurrent refreshes of the same token — a SPA firing two calls after one
    /// <c>401</c> as much as an attacker racing the legitimate client — must not both succeed: that would split the
    /// family into two live chains with no replay detected, defeating the detection the whole design exists for.
    /// Exactly one wins; the loser is indistinguishable from a replay, because that is what it looks like.
    /// </summary>
    [Fact]
    public async Task Test_Concurrent_Refresh_Of_One_Token_Yields_Exactly_One_Winner()
    {
        var (service, _) = Create();
        var issued = await service.Issue(UserId, Claims());

        var attempts = await Task.WhenAll(Enumerable.Range(0, 8)
            .Select(_ => service.Refresh(issued.RefreshToken!, Resolver())));

        attempts.Count(pair => pair != null).ShouldBe(1);
    }

    /// <summary>
    /// The losers of that race are treated as replays, so the family is revoked and the winner's token is dead too —
    /// the same response a genuine stolen-token replay gets, because the two are indistinguishable from the server.
    /// </summary>
    [Fact]
    public async Task Test_Losing_A_Refresh_Race_Ends_The_Chain()
    {
        var (service, _) = Create();
        var issued = await service.Issue(UserId, Claims());

        var attempts = await Task.WhenAll(Enumerable.Range(0, 4)
            .Select(_ => service.Refresh(issued.RefreshToken!, Resolver())));
        var winner = attempts.Single(pair => pair != null)!;

        (await service.Refresh(winner.RefreshToken!, Resolver())).ShouldBeNull();
    }

    private static (IRefreshTokenService Service, IRefreshTokenStore Store) Create(Action<RefreshTokenOptions>? configure = null)
    {
        var options = new RefreshTokenOptions();
        configure?.Invoke(options);

        var jwtOptions = new JwtTokenOptions { Secret = Secret, Audience = "spa", Authority = "https://issuer.example" };
        var store = new InMemoryRefreshTokenStore();

        return (new RefreshTokenService(new JwtTokenHelper(jwtOptions), store, jwtOptions, options), store);
    }

    private static Claim[] Claims(string role = "admin") =>
    [
        new(RegiraClaimTypesSubject, UserId),
        new(RegiraClaimTypesRole, role)
    ];

    private static Func<string, Task<IEnumerable<Claim>?>> Resolver(string role = "admin")
        => _ => Task.FromResult<IEnumerable<Claim>?>(Claims(role));

    private static string[] ReadRoles(string accessToken)
        => new System.IdentityModel.Tokens.Jwt.JwtSecurityTokenHandler()
            .ReadJwtToken(accessToken).Claims
            .Where(claim => claim.Type == RegiraClaimTypesRole)
            .Select(claim => claim.Value)
            .ToArray();

    private const string RegiraClaimTypesSubject = "sub";
    private const string RegiraClaimTypesRole = "role";
}
