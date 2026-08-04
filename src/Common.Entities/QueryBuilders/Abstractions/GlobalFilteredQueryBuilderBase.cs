using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.QueryBuilders.Abstractions;

public abstract class GlobalFilteredQueryBuilderBase<TEntity> : GlobalFilteredQueryBuilderBase<TEntity, int>;
public abstract class GlobalFilteredQueryBuilderBase<TEntity, TKey> : FilteredQueryBuilderBase<TEntity, TKey, ISearchObject<TKey>>,
    IGlobalFilteredQueryBuilder<TEntity, TKey>
{
    IQueryable<TEntity> IGlobalFilteredQueryBuilder<TEntity, TKey>.Build(IQueryable<TEntity> query, ISearchObject<TKey>? so)
        => Build(query, so);
    IQueryable<T> IGlobalFilteredQueryBuilder.Build<T, TK>(IQueryable<T> query, ISearchObject<TK>? so)
        // A search object of a foreign key type coerces to null — the filter then applies its key-agnostic
        // default (e.g. hide archived rows). This must NOT step aside and return the query unfiltered, or a
        // soft-delete/security default would be silently dropped. Which variant of a filter family runs is
        // decided upstream in QueryBuilder.Filter (it prefers the key-matching variant), so this variant
        // only ever runs when it is the right one, or as the sole (safe-default) variant of its family.
        => Build(query.Cast<TEntity>(), so as ISearchObject<TKey>).Cast<T>();
}
