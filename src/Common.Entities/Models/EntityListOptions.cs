namespace Regira.Entities.Models;

/// <summary>
/// Runtime carrier for the list/paging options configured at <c>UseEntities()</c>.
/// Registered as a singleton and resolved by the List/Search endpoints to apply a default and/or maximum
/// page size. Both values are optional; <c>null</c> disables that aspect.
/// </summary>
public class EntityListOptions
{
    /// <summary>Default page size applied when the caller omits paging (<c>PageSize</c> is <c>null</c>). <c>null</c> = no default (an omitted request is then bounded only by <see cref="MaxPageSize"/>).</summary>
    public int? DefaultPageSize { get; set; }
    /// <summary>Upper limit a page size is clamped to, and the size a <c>pageSize &lt;= 0</c> opt-out falls back to. <c>null</c> means no limit.</summary>
    public int? MaxPageSize { get; set; }
}

/// <summary>
/// Per-entity override of the global <see cref="EntityListOptions"/>, configured via
/// <c>SetPageSize(...)</c> on the entity service builder. When registered, it fully defines that
/// entity's paging (each <c>null</c> aspect is off, with no fallback to the global options).
/// </summary>
/// <typeparam name="TEntity">The entity this override applies to.</typeparam>
public class EntityListOptions<TEntity> : EntityListOptions where TEntity : class;
