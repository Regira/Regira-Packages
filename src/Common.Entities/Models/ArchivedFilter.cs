namespace Regira.Entities.Models;

/// <summary>
/// Which rows of an <see cref="Models.Abstractions.IArchivable"/> entity a query returns.
/// Set per request through <see cref="Models.Abstractions.ISearchObject.Archived"/>; when that is
/// <c>null</c> the configured <see cref="EntityQueryOptions.DefaultArchivedFilter"/> applies.
/// </summary>
public enum ArchivedFilter
{
    /// <summary>Archived rows are invisible — the default, and what makes <c>DELETE</c> a soft delete.</summary>
    Excluded = 0,
    /// <summary>Live and archived rows alike.</summary>
    Included,
    /// <summary>Archived rows only.</summary>
    Only
}
