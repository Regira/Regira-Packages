using Regira.Security.Authentication.Core.Models;
using System.Security.Claims;

namespace Regira.Security.Authentication.Core.Services;

/// <summary>
/// Gives a principal the <see cref="RegiraClaimTypes"/> spellings alongside whatever its provider emitted.
/// <para>
/// <b>Additive, never substitutive.</b> Every source claim survives untouched and a canonical copy is added when
/// one is missing. Folding a provider's claim into the canonical name and dropping the original would break any
/// consumer reading it directly — Entra's <c>roles</c> being the obvious case — whereas adding a <c>role</c>
/// copy cannot break anything except code that counts claims, and is what makes
/// <c>[Authorize(Roles = …)]</c> start resolving against an Entra token.
/// </para>
/// </summary>
public static class ClaimsNormalizer
{
    /// <summary>
    /// A <see cref="ClaimsIdentity"/> carrying <paramref name="source"/> plus any missing canonical claims, with
    /// its name and role types set to the canonical spellings.
    /// <para>
    /// Setting those two on the identity is the load-bearing part: <see cref="ClaimsPrincipal.IsInRole"/> and
    /// <see cref="System.Security.Principal.IIdentity.Name"/> resolve through the identity's <em>own</em>
    /// configured types, so a canonical claim without them would be inert.
    /// </para>
    /// </summary>
    public static ClaimsIdentity Normalize(IEnumerable<Claim> source, string authenticationType, ClaimNormalizationOptions? options = null)
    {
        options ??= new ClaimNormalizationOptions();

        var claims = source.ToList();

        AddCanonicalSingle(claims, RegiraClaimTypes.Subject, options.SubjectClaimTypes);
        AddCanonicalSingle(claims, RegiraClaimTypes.Name, options.NameClaimTypes);
        AddCanonicalSingle(claims, RegiraClaimTypes.Email, options.EmailClaimTypes);
        AddCanonicalMultiple(claims, RegiraClaimTypes.Role, options.RoleClaimTypes);

        return new ClaimsIdentity(claims, authenticationType, RegiraClaimTypes.Name, RegiraClaimTypes.Role);
    }

    /// <summary>
    /// Adds one canonical claim from the first source type that carries a value. Does nothing when the canonical
    /// type is already present — a provider that spells it canonically is left exactly as it was.
    /// </summary>
    private static void AddCanonicalSingle(List<Claim> claims, string canonicalType, IEnumerable<string> sourceTypes)
    {
        if (claims.Any(claim => Matches(claim, canonicalType)))
        {
            return;
        }

        var value = sourceTypes
            .Select(sourceType => claims.FirstOrDefault(claim => Matches(claim, sourceType))?.Value)
            .FirstOrDefault(candidate => !string.IsNullOrWhiteSpace(candidate));

        if (value != null)
        {
            claims.Add(new Claim(canonicalType, value));
        }
    }

    /// <summary>
    /// Adds a canonical claim for every distinct value across all source types — roles are a set, not a single
    /// value, and a principal can legitimately carry them under two spellings at once.
    /// </summary>
    private static void AddCanonicalMultiple(List<Claim> claims, string canonicalType, IEnumerable<string> sourceTypes)
    {
        var existing = claims
            .Where(claim => Matches(claim, canonicalType))
            .Select(claim => claim.Value)
            .ToHashSet(StringComparer.Ordinal);

        var values = sourceTypes
            .SelectMany(sourceType => claims.Where(claim => Matches(claim, sourceType)))
            .Select(claim => claim.Value)
            .Where(value => !string.IsNullOrWhiteSpace(value) && existing.Add(value))
            .ToArray();

        claims.AddRange(values.Select(value => new Claim(canonicalType, value)));
    }

    /// <summary>
    /// Claim types are compared case-insensitively, the same way <see cref="ClaimsIdentity.FindFirst(string)"/>
    /// does — a provider capitalising <c>Role</c> must not produce a second, separate claim.
    /// </summary>
    private static bool Matches(Claim claim, string claimType) => string.Equals(claim.Type, claimType, StringComparison.OrdinalIgnoreCase);
}
