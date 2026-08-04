using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Models;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;

public class FilterArchivablesQueryBuilder(EntityQueryOptions? queryOptions = null) : FilterArchivablesQueryBuilder<int>(queryOptions);

/// <summary>
/// Translates <see cref="ISearchObject.Archived"/> (or the configured
/// <see cref="EntityQueryOptions.DefaultArchivedFilter"/>) onto the query. On <c>net10.0</c> archived rows
/// are hidden by the named EF query filter from <see cref="ModelBuilderExtensions.SetArchivedQueryFilter"/>,
/// so the default composes nothing and the opt-ins suspend that one filter by name; on <c>net8.0</c> no
/// query filter exists and this composes the predicate itself. See
/// <see cref="QueryExtensions.FilterArchivable{TEntity}"/> for the full per-target contract.
/// </summary>
public class FilterArchivablesQueryBuilder<TKey>(EntityQueryOptions? queryOptions = null) : GlobalFilteredQueryBuilderBase<IArchivable, TKey>
{
    // The only reader of ISearchObject.Archived. Every other global filter in the aggregate loop
    // (tenant/owner row security) ignores it and keeps filtering, and none of them is an EF query filter,
    // so opting into archived rows can never widen the result beyond the archived flag.
    public override IQueryable<IArchivable> Build(IQueryable<IArchivable> query, ISearchObject<TKey>? so)
        => query.FilterArchivable(so?.Archived ?? queryOptions?.DefaultArchivedFilter ?? ArchivedFilter.Excluded);
}
