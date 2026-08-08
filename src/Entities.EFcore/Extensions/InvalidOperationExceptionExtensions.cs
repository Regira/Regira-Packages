namespace Regira.Entities.EFcore.Extensions;

public static class InvalidOperationExceptionExtensions
{
    /// <summary>
    /// True when EF Core's change tracker refused to null a non-nullable foreign key while fixing up a
    /// severed required relationship — the client-side counterpart of a database FK violation, thrown
    /// before the provider is ever reached (so it is never a <see cref="Microsoft.EntityFrameworkCore.DbUpdateException"/>).<br />
    /// Raised when a required-FK dependent is tracked and its principal is deleted under
    /// <c>DeleteBehavior.Restrict</c>; a hierarchy that eager-loads its own children hits it on every
    /// parent delete. Matched on EF Core's invariant message text, which its resources do not localise.
    /// </summary>
    public static bool IsSeveredRequiredRelationship(this InvalidOperationException ex)
        => ex.Message.Contains("has been severed", StringComparison.Ordinal);
}
