using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.EFcore.Preppers;
using Regira.Entities.Preppers;
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
                p.GetRequiredService<TContext>(), navigationExpression, NestedPreppers.WithServerOwned(nestedPreppers));
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

    /// <summary>
    /// Declares <paramref name="selector"/> as owned by the server on this owned child: restored from the
    /// stored row on update, so a parent payload cannot change it (a line's <c>UnitPrice</c> resolved from
    /// the product, never taken from the request). A <c>[ServerOwned]</c> attribute on the child is enforced
    /// here too, without this call.
    /// </summary>
    /// <param name="selector">The property to protect, e.g. <c>x =&gt; x.UnitPrice</c>. Scalars and FKs only.</param>
    /// <param name="mintOnCreate">Mints the value on create when the property is unset. Omit to protect only.</param>
    public RelatedEntityBuilder<TContext, TRelated, TRelatedKey> ServerOwned<TProp>(Expression<Func<TRelated, TProp>> selector, Func<TRelated, TProp>? mintOnCreate = null)
    {
        var prepper = new ServerOwnedPrepper<TRelated, TProp>(selector, mintOnCreate);
        PrepperFactories.Add(_ => prepper);
        return this;
    }
}
