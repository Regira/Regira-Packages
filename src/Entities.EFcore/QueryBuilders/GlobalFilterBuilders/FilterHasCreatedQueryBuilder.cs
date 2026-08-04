using Regira.Entities.EFcore.Extensions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;

public class FilterHasCreatedQueryBuilder : FilterHasCreatedQueryBuilder<int>;
public class FilterHasCreatedQueryBuilder<TKey> : GlobalFilteredQueryBuilderBase<IHasCreated, TKey>
{
    public override IQueryable<IHasCreated> Build(IQueryable<IHasCreated> query, ISearchObject<TKey>? so) 
        => query.FilterCreated(so?.MinCreated, so?.MaxCreated);
}