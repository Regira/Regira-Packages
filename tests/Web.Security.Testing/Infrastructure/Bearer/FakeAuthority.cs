using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Regira.Security.Authentication.Jwt.Models;
using System.Security.Cryptography;

namespace Web.Security.Testing.Infrastructure.Bearer;

/// <summary>
/// Stands in for Entra: one RSA key pair, shared between the token this mints and the scheme that validates it.
/// <para>
/// The point is to exercise the <b>asymmetric</b> path, which is what an external authority actually uses and what
/// <c>AddJwtAuthentication</c> cannot do at all — it derives a symmetric key from a secret. Tokens are shaped the
/// way Entra shapes them: <c>oid</c> for identity, <c>roles</c> as a JSON array, <c>tid</c> for the tenant.
/// </para>
/// </summary>
public static class FakeAuthority
{
    public const string TenantId = "11111111-2222-3333-4444-555555555555";
    public const string OtherTenantId = "99999999-8888-7777-6666-555555555555";
    public const string ClientId = "aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee";
    public const string ObjectId = "ENTRA_OBJECT_ID";
    public const string UserName = "someone@contoso.com";
    public const string AdminRole = "admin";

    private static readonly RSA Rsa = RSA.Create(2048);

    public static RsaSecurityKey SigningKey { get; } = new(Rsa) { KeyId = "fake-authority-key" };

    public static string V2Issuer(string tenantId = TenantId)
        => $"{EntraIdDefaults.Instance}/{tenantId}/v2.0";

    /// <summary>The issuer a registration that has not opted into v2 tokens uses.</summary>
    public static string V1Issuer(string tenantId = TenantId)
        => $"{EntraIdDefaults.V1IssuerHost}/{tenantId}/";

    /// <summary>The audience form Entra emits when the client requested the scope by App ID URI.</summary>
    public static string ScopedAudience => $"api://{ClientId}";

    public static string CreateToken(string? issuer = null, string? audience = null, string? tenantId = null, bool withRole = true, TimeSpan? lifetime = null)
    {
        var claims = new Dictionary<string, object>
        {
            ["oid"] = ObjectId,
            ["sub"] = "PAIRWISE_SUBJECT_DO_NOT_KEY_ON_THIS",
            ["preferred_username"] = UserName,
            ["tid"] = tenantId ?? TenantId,
            ["scp"] = "openid profile api.read"
        };
        if (withRole)
        {
            // An array, the way Entra emits app roles — each element must reach the principal as its own claim.
            claims["roles"] = new[] { AdminRole };
        }

        var descriptor = new SecurityTokenDescriptor
        {
            Issuer = issuer ?? V2Issuer(),
            Audience = audience ?? ScopedAudience,
            Claims = claims,
            Expires = DateTime.UtcNow.Add(lifetime ?? TimeSpan.FromMinutes(5)),
            SigningCredentials = new SigningCredentials(SigningKey, SecurityAlgorithms.RsaSha256)
        };

        return new JsonWebTokenHandler().CreateToken(descriptor);
    }
}
