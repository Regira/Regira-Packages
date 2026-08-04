using Duende.IdentityModel;
using Microsoft.AspNetCore.Authentication.JwtBearer;

namespace Regira.Security.Authentication.Jwt.Models;

public class JwtTokenOptions
{
    public string Secret { get; set; } = null!;
    public string? Algorithm { get; set; }

    /// <summary>
    /// Whether registration rejects a <see cref="Secret"/> too short for <see cref="Algorithm"/> (default
    /// <c>true</c>). The HMAC algorithms need a key at least as long as their hash — 64 bytes for the
    /// <c>HS512</c> default — and a shorter one fails at the first token creation, not at startup.
    /// <para>
    /// Set to <c>false</c> for a scheme that only ever <em>validates</em> tokens signed elsewhere: the
    /// algorithm the token declares governs there, so the length required of the local secret is whatever the
    /// issuer used, which <see cref="Algorithm"/> does not describe.
    /// </para>
    /// </summary>
    public bool ValidateSecretLength { get; set; } = true;
    public string AuthenticationScheme { get; set; } = JwtBearerDefaults.AuthenticationScheme;

    public string? Authority { get; set; }
    public string? Audience { get; set; }
    public ICollection<string>? Audiences { get; set; }

    /// <summary>
    /// Token lifespan in seconds (default 2 hours)
    /// </summary>
    public int LifeSpan { get; set; } = 60 * 60 * 2;
    public bool IncludeIssuedDate { get; set; } = true;

    public string NameClaimType { get; set; } = JwtClaimTypes.Name;
    public string RoleClaimType { get; set; } = JwtClaimTypes.Role;

    public bool UseJwtClaimTypes { get; set; } = true;
}