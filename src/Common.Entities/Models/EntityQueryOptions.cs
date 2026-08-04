namespace Regira.Entities.Models;

/// <summary>
/// Runtime carrier for the query-pipeline behavior configured at <c>UseEntities()</c>.
/// Registered as a singleton and resolved by the query builders.
/// </summary>
public class EntityQueryOptions
{
    /// <summary>
    /// Which rows of an <c>IArchivable</c> entity a query returns when the search object leaves
    /// <see cref="Models.Abstractions.ISearchObject.Archived"/> null. Defaults to
    /// <see cref="ArchivedFilter.Excluded"/> (archived rows invisible).
    /// </summary>
    public ArchivedFilter DefaultArchivedFilter { get; set; }
}
