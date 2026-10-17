using Microsoft.EntityFrameworkCore;
using Regira.Entities.DependencyInjection.ServiceBuilders.Abstractions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceBuilders;

[Obsolete("Nothing constructs it: For<TEntity, int, TSearchObject>() hands out EntitySearchObjectServiceBuilder. Will be removed in v7.")]
public class EntityIntSearchObjectServiceBuilder<TContext, TEntity, TSearchObject>(EntityServiceCollectionOptions options)
    : EntitySearchObjectServiceBuilder<TContext, TEntity, int, TSearchObject>(options), IEntityServiceBuilder<TContext, TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity<int>
    where TSearchObject : class, ISearchObject<int>, new()
{
}
