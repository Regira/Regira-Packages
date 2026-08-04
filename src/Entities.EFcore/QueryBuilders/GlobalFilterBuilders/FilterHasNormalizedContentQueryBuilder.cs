using Microsoft.EntityFrameworkCore;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Keywords.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;

public class FilterHasNormalizedContentQueryBuilder(IQKeywordHelper qHelper) : FilterHasNormalizedContentQueryBuilder<int>(qHelper);
public class FilterHasNormalizedContentQueryBuilder<TKey>(IQKeywordHelper qHelper) : GlobalFilteredQueryBuilderBase<IHasNormalizedContent, TKey>
{
    public override IQueryable<IHasNormalizedContent> Build(IQueryable<IHasNormalizedContent> query, ISearchObject<TKey>? so)
    {
        if (!string.IsNullOrWhiteSpace(so?.Q))
        {
            var keywords = qHelper.Parse(so.Q);
            foreach (var q in keywords)
            {
                query = query.Where(x => EF.Functions.Like(x.NormalizedContent, q.QW));
            }
        }

        return query;
    }
}