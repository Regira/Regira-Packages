using Microsoft.EntityFrameworkCore;
using Regira.Entities.Preppers.Abstractions;

namespace Regira.Entities.EFcore.Preppers;

public class EntityPrepper<TEntity>(Action<TEntity> prepareFunc) : EntityPrepperBase<TEntity>
    where TEntity : class
{
    public override Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default)
    {
        prepareFunc(modified);
        return Task.CompletedTask;
    }
}

public class EntityPrepper<TContext, TEntity>(TContext dbContext, Func<TEntity, TContext, Task> prepareFunc) : EntityPrepperBase<TEntity>
    where TContext : DbContext
    where TEntity : class
{
    public override Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default) 
        => prepareFunc(modified, dbContext);
}