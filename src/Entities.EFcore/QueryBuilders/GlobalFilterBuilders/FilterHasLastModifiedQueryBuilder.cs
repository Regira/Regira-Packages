using Regira.Entities.EFcore.Extensions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;

public class FilterHasLastModifiedQueryBuilder : FilterHasLastModifiedQueryBuilder<int>;
public class FilterHasLastModifiedQueryBuilder<TKey> : GlobalFilteredQueryBuilderBase<IHasLastModified, TKey>
{
    public override IQueryable<IHasLastModified> Build(IQueryable<IHasLastModified> query, ISearchObject<TKey>? so) 
        => query.FilterLastModified(so?.MinLastModified, so?.MaxLastModified);
}