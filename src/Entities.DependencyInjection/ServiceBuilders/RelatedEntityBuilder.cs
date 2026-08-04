using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Models.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

public class RelatedEntityBuilder<TContext, TRelated, TRelatedKey>
    where TContext : DbContext
    where TRelated : class, IEntity<TRelatedKey>
{
    internal List<Func<IServiceProvider, IEntityPrepper<TRelated>>> PrepperFactories { get; } = [];

    public RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Related<TSubRelated, TSubRelatedKey>(
        Expression<Func<TRelated, ICollection<TSubRelated>?>> navigationExpression,
        Action<RelatedEntityBuilder<TContext, TSubRelated, TSubRelatedKey>>? configure = null)
        where TSubRelated : class, IEntity<TSubRelatedKey>
    {
        PrepperFactories.Add(p =>
        {
            IEnumerable<IEntityPrepper<TSubRelated>>? nestedPreppers = null;
            if (configure != null)
            {
                var subBuilder = new RelatedEntityBuilder<TContext, TSubRelated, TSubRelatedKey>();
                configure(subBuilder);
                nestedPreppers = subBuilder.PrepperFactories.Select(f => f(p));
            }

            return new RelatedCollectionPrepper<TContext, TRelated, TSubRelated, TRelatedKey, TSubRelatedKey>(
                p.GetRequiredService<TContext>(), navigationExpression, nestedPreppers);
        });

        return this;
    }

    public RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Related<TSubRelated>(
        Expression<Func<TRelated, ICollection<TSubRelated>?>> navigationExpression,
        Action<RelatedEntityBuilder<TContext, TSubRelated, int>>? configure = null)
        where TSubRelated : class, IEntity<int>
        => Related<TSubRelated, int>(navigationExpression, configure);

    public RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Related<TSubRelated, TSubRelatedKey>(
        Expression<Func<TRelated, ICollection<TSubRelated>?>> navigationExpression,
        Action<TRelated> prepareFunc,
        Action<RelatedEntityBuilder<TContext, TSubRelated, TSubRelatedKey>>? configure = null)
        where TSubRelated : class, IEntity<TSubRelatedKey>
    {
        Related(navigationExpression, configure);
        Prepare(prepareFunc);
        return this;
    }

    public RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Related<TSubRelated>(
        Expression<Func<TRelated, ICollection<TSubRelated>?>> navigationExpression,
        Action<TRelated> prepareFunc,
        Action<RelatedEntityBuilder<TContext, TSubRelated, int>>? configure = null)
        where TSubRelated : class, IEntity<int>
        => Related<TSubRelated, int>(navigationExpression, prepareFunc, configure);

    public RelatedEntityBuilder<TContext, TRelated, TRelatedKey> Prepare(Action<TRelated> prepareFunc)
    {
        PrepperFactories.Add(_ => new EntityPrepper<TRelated>(prepareFunc));
        return this;
    }
}
