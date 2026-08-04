using Duende.IdentityModel;
using System.Security.Claims;

namespace Regira.Security.Authentication.Core.Models;

/// <summary>
/// Which incoming claim types stand in for each of the <see cref="RegiraClaimTypes"/>. Each list is searched in
/// order and the first non-empty match wins; roles take every match.
/// <para>
/// The defaults cover what the schemes in this package and the identity providers they talk to actually emit —
/// <c>sub</c> and <c>oid</c> for a subject, <c>roles</c> plural for Entra app roles, the long
/// <see cref="ClaimTypes"/> URIs for anything built by ASP.NET Identity or the API-key handler.
/// </para>
/// </summary>
public class ClaimNormalizationOptions
{
    /// <summary>
    /// Entra puts the stable per-tenant user id in <c>oid</c>; its <c>sub</c> is pairwise per application, so two
    /// apps see different values for the same person. <c>sub</c> is listed first because a self-issued token has
    /// nothing else, but an Entra principal carries both and <c>oid</c> is the one to key a row on.
    /// </summary>
    public IList<string> SubjectClaimTypes { get; } = [ClaimTypes.NameIdentifier, JwtClaimTypes.Subject, "oid"];

    public IList<string> NameClaimTypes { get; } =
        [ClaimTypes.Name, JwtClaimTypes.Name, JwtClaimTypes.PreferredUserName, "upn", "unique_name"];

    public IList<string> EmailClaimTypes { get; } = [ClaimTypes.Email, JwtClaimTypes.Email, "upn"];

    /// <summary><c>roles</c> is Entra's spelling for app roles — <c>role</c> singular matches nothing there.</summary>
    public IList<string> RoleClaimTypes { get; } = [JwtClaimTypes.Role, "roles", ClaimTypes.Role];

    /// <summary>
    /// Merges this instance's entries into <paramref name="target"/>, preserving the caller's order — anything the
    /// caller added to a list comes first, so it wins the "first non-empty match" search.
    /// <para>
    /// Exists because the lists are get-only and every preset builds a fresh options object to expand into. Copying
    /// them one by one is how a customised list silently goes missing.
    /// </para>
    /// </summary>
    public void MergeInto(ClaimNormalizationOptions target)
    {
        Merge(SubjectClaimTypes, target.SubjectClaimTypes);
        Merge(NameClaimTypes, target.NameClaimTypes);
        Merge(EmailClaimTypes, target.EmailClaimTypes);
        Merge(RoleClaimTypes, target.RoleClaimTypes);
    }

    private static void Merge(IList<string> source, IList<string> target)
    {
        var index = 0;
        foreach (var claimType in source)
        {
            if (target.Contains(claimType, StringComparer.OrdinalIgnoreCase))
            {
                continue;
            }

            target.Insert(index++, claimType);
        }
    }
}
