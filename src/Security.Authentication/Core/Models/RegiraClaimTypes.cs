using Duende.IdentityModel;

namespace Regira.Security.Authentication.Core.Models;

/// <summary>
/// The claim types every Regira scheme guarantees on a normalized principal.
/// <para>
/// These are the JWT spellings, deliberately: <c>JwtTokenOptions.NameClaimType</c> and
/// <c>JwtTokenOptions.RoleClaimType</c> already default to them, so a normalized principal is
/// indistinguishable from a JWT one to <c>[Authorize(Roles = …)]</c>, <c>User.IsInRole</c> <em>and</em> a
/// hand-written <c>RequireClaim("role", …)</c> — the three checks that disagree when a scheme picks its own
/// spelling.
/// </para>
/// </summary>
public static class RegiraClaimTypes
{
    /// <summary>Subject — the caller's stable identifier.</summary>
    public const string Subject = JwtClaimTypes.Subject;

    public const string Name = JwtClaimTypes.Name;
    public const string Email = JwtClaimTypes.Email;
    public const string Role = JwtClaimTypes.Role;
}
