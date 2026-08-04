using Regira.Entities.EFcore.Extensions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;

public class FilterIdsQueryBuilder : FilterIdsQueryBuilder<int>;
public class FilterIdsQueryBuilder<TKey> : GlobalFilteredQueryBuilderBase<IEntity<TKey>, TKey>
{
    public override IQueryable<IEntity<TKey>> Build(IQueryable<IEntity<TKey>> query, ISearchObject<TKey>? so)
    {
        if (so != null)
        {
            query = query.FilterId(so.Id);
            query = query.FilterIds(so.Ids);
            query = query.FilterExclude(so.Exclude);
        }

        return query;
    }
}