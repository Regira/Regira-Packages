using Microsoft.EntityFrameworkCore;
using Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

public class EntityIntSearchObjectServiceBuilder<TContext, TEntity, TSearchObject>(EntityServiceCollectionOptions options)
    : EntitySearchObjectServiceBuilder<TContext, TEntity, int, TSearchObject>(options), IEntityServiceBuilder<TContext, TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
{
    // Build
    public override void Build()
    {
        base.Build();

        // Query Builder
        if (!HasService<IQueryBuilder<TEntity, int, TSearchObject, EntitySortBy, EntityIncludes>>())
        {
            AddDefaultQueryBuilder();
        }
        // Read Service
        if (!HasService<IEntityReadService<TEntity, int, TSearchObject>>())
        {
            UseReadService<EntityReadService<TContext, TEntity, int, TSearchObject>>();
        }
        // Write Service
        if (!HasService<IEntityWriteService<TEntity, int>>())
        {
            UseWriteService<EntityWriteService<TContext, TEntity, int>>();
        }
        // Entity Repository
        if (!HasService<IEntityRepository<TEntity, int, TSearchObject>>())
        {
            HasRepositoryInner<EntityRepository<TEntity, int, TSearchObject>>();
        }
        // Entity Service
        if (!HasService<IEntityService<TEntity, int, TSearchObject>>())
        {
            UseEntityService<EntityRepository<TEntity, int, TSearchObject>>();
        }
    }
}