using Microsoft.AspNetCore.Authentication.JwtBearer;
using Regira.Security.Authentication.Core.Models;

namespace Regira.Security.Authentication.Jwt.Models;

/// <summary>
/// Validating bearer tokens that something else issued — Entra ID, Auth0, Keycloak, Duende, Okta.
/// <para>
/// Separate from <see cref="JwtTokenOptions"/> because that type conflates issuing with validating: it requires a
/// <c>Secret</c> and always derives a symmetric key, which cannot validate an authority that signs with rotating
/// asymmetric keys published at a JWKS endpoint. Registering these options registers <b>no</b>
/// <c>ITokenHelper</c> — this scheme reads tokens, it does not mint them.
/// </para>
/// </summary>
public class BearerValidationOptions
{
    public string AuthenticationScheme { get; set; } = JwtBearerDefaults.AuthenticationScheme;

    /// <summary>
    /// The issuer's base URL. Signing keys are discovered from <c>{Authority}/.well-known/openid-configuration</c>
    /// and refreshed as the authority rotates them.
    /// </summary>
    public string? Authority { get; set; }

    /// <summary>Overrides the metadata URL derived from <see cref="Authority"/>.</summary>
    public string? MetadataAddress { get; set; }

    /// <summary>
    /// A shared symmetric key, for an issuer that signs with HMAC instead of publishing a JWKS. Mutually
    /// exclusive with <see cref="Authority"/> / <see cref="MetadataAddress"/>.
    /// </summary>
    public string? Secret { get; set; }

    public string? Audience { get; set; }

    /// <summary>Takes precedence over <see cref="Audience"/> when set.</summary>
    public ICollection<string>? Audiences { get; set; }

    /// <summary>
    /// Accepted issuers. Left null, the issuer advertised by the discovery document is used — correct for a
    /// single-tenant authority and <b>wrong for a multi-tenant one</b>, where each tenant issues under its own id.
    /// </summary>
    public ICollection<string>? ValidIssuers { get; set; }

    /// <summary>
    /// ⚠️ Leave on outside local development. Off, the host will fetch signing keys over plain HTTP, which lets
    /// anything on the path decide who is allowed in.
    /// </summary>
    public bool RequireHttpsMetadata { get; set; } = true;

    /// <summary>Keep the raw token in the authentication properties, for calling a downstream API with it.</summary>
    public bool SaveToken { get; set; }

    public bool ValidateLifetime { get; set; } = true;

    /// <summary>Zero, matching the rest of the package: a token expires exactly when it says it does.</summary>
    public TimeSpan ClockSkew { get; set; } = TimeSpan.Zero;

    public string NameClaimType { get; set; } = RegiraClaimTypes.Name;

    /// <summary>
    /// ⚠️ Provider-specific. Entra spells app roles <c>roles</c> — <c>role</c> singular matches nothing there, and
    /// the symptom is a 403 against a token that visibly contains the role.
    /// </summary>
    public string RoleClaimType { get; set; } = RegiraClaimTypes.Role;

    /// <summary>Source claim types folded into the canonical set once the token validates.</summary>
    public ClaimNormalizationOptions Claims { get; } = new();

    /// <summary>
    /// Applied last, for anything not exposed here. Replacing <c>Events</c> wholesale is safe — the normalization
    /// hook is chained after whatever this leaves behind, so it cannot be dropped by accident.
    /// </summary>
    public Action<JwtBearerOptions>? Configure { get; set; }
}
