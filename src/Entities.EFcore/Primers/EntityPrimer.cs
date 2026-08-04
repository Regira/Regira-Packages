using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;

namespace Regira.Entities.EFcore.Primers;

public class EntityPrimer<TEntity>(Func<TEntity, EntityEntry, Task> primeFunc) : EntityPrimerBase<TEntity>
    where TEntity : class
{
    public override Task PrepareAsync(TEntity entity, EntityEntry entry, CancellationToken token = default)
        => primeFunc.Invoke(entity, entry);
}

public class EntityPrimer<TContext, TEntity>(TContext dbContext, Func<TEntity, EntityEntry, TContext, Task> primeFunc) : EntityPrimerBase<TEntity>
    where TContext : DbContext
    where TEntity : class
{
    public override Task PrepareAsync(TEntity entity, EntityEntry entry, CancellationToken token = default)
        => primeFunc.Invoke(entity, entry, dbContext);
}