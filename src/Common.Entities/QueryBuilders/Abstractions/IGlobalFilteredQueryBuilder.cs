using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.QueryBuilders.Abstractions;

public interface IGlobalFilteredQueryBuilder
{
    IQueryable<TEntity> Build<TEntity, TKey>(IQueryable<TEntity> query, ISearchObject<TKey>? so);
}

public interface IGlobalFilteredQueryBuilder<TEntity, TKey> : IGlobalFilteredQueryBuilder
{
    IQueryable<TEntity> Build(IQueryable<TEntity> query, ISearchObject<TKey>? so);
}

public interface IGlobalFilteredQueryBuilder<TEntity> : IGlobalFilteredQueryBuilder<TEntity, int>;
