using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.EFcore.Extensions;

public static class DbContextExtensions
{
    #region Primers
    /// <summary>
    /// Applies <see cref="IEntityPrimer{T}"/> without saving to DB<br />
    /// Make sure <see cref="EntityPrimerContainer"/> is registered in <see cref="IServiceCollection"/>, by calling <see cref="EntityPrimerContainerExtensions.RegisterPrimerContainer{TContext}"/> extension method
    /// </summary>
    /// <param name="dbContext"></param>
    /// <param name="entityType"></param>
    /// <param name="token"></param>
    /// <returns></returns>
    public static Task ApplyPrimers(this DbContext dbContext, Type? entityType = null, CancellationToken token = default)
    {
        var primerContainer = dbContext.GetService<EntityPrimerContainer>();
        return primerContainer.ApplyPrimers(entityType, token);
    }
    public static Task ApplyPrimers<T>(this DbContext dbContext, CancellationToken token = default)
        => dbContext.ApplyPrimers(typeof(T), token);
    #endregion


    #region ChildCollections
    public static void UpdateRelatedCollection<TEntity, TRelated>(this DbContext dbContext, TEntity modified, TEntity original, Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression)
        where TEntity : class, IEntity<int>
        where TRelated : class, IEntity<int>
    {
        foreach (var item in navigationExpression.Compile()(modified) ?? Array.Empty<TRelated>())
        {
            // No negative ids for EF!
            item.Id = Math.Max(0, item.Id);
        }
        dbContext.UpdateRelatedCollection<TEntity, TRelated, int, int>(modified, original, navigationExpression);
    }

    public static void UpdateRelatedCollection<TEntity, TRelated, TEntityKey, TRelatedKey>(this DbContext dbContext, TEntity modified, TEntity original, Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression)
        where TEntity : class, IEntity<TEntityKey>
        where TRelated : class, IEntity<TRelatedKey>
    {
        var selectorFunc = navigationExpression.Compile();
        var originalItems = selectorFunc(original);
        var modifiedItems = selectorFunc(modified);

        dbContext.UpdateRelatedCollection<TEntity, TRelated, TEntityKey, TRelatedKey>(modified, modifiedItems, original, originalItems);
    }
    public static void UpdateRelatedCollection<TEntity, TRelated, TEntityKey, TRelatedKey>(this DbContext dbContext, TEntity modified, ICollection<TRelated>? modifiedItems, TEntity original, ICollection<TRelated>? originalItems)
        where TEntity : class, IEntity<TEntityKey>
        where TRelated : class, IEntity<TRelatedKey>
    {
        if (modifiedItems == null || originalItems == null)
        {
            return;
        }

        var relatedItemsToAdd = modifiedItems.Where(m => m.Id == null || m.Id.Equals(default(TRelatedKey)) || originalItems.All(o => m.Id.Equals(o.Id) != true)).ToArray();
        var relatedItemsToDelete = originalItems.Where(o => modifiedItems.All(m => m.Id != null && m.Id.Equals(o.Id) != true)).ToArray();
        // Materialize before the delete loop so EF Core navigation fixup cannot add deleted items back into modifiedItems
        var relatedItemsToModify = modifiedItems.Except(relatedItemsToAdd).Where(m => m.Id != null && !m.Id.Equals(default(TRelatedKey))).ToArray();
        foreach (var entity in relatedItemsToAdd)
        {
            // Clear a client-minted temp key before inserting. Front-end editors hand new rows a NEGATIVE id
            // so they have a stable :key before the server has seen them; such a row matches no original, so
            // it correctly lands here — but attaching it as Added with the temp key still on it makes the
            // provider insert that key verbatim (SQLite accepts a negative rowid without complaint), and the
            // row is only visibly wrong to someone who inspects primary keys. Same normalization the int
            // convenience overload above already applies, generalized to any signed numeric key.
            ResetTempKey<TRelated, TRelatedKey>(entity);
            dbContext.Entry(entity).State = EntityState.Added;
        }
        foreach (var entity in relatedItemsToDelete)
        {
            dbContext.Entry(entity).State = EntityState.Deleted;
        }
        foreach (var entity in relatedItemsToModify)
        {
            var originalEntity = originalItems.Single(p => p.Id!.Equals(entity.Id));
            dbContext.Entry(originalEntity).State = EntityState.Detached;
            dbContext.Attach(entity);
            dbContext.Entry(entity).OriginalValues.SetValues(originalEntity);
            dbContext.Entry(entity).State = EntityState.Modified;
        }
    }

    /// <summary>
    /// Zeroes a negative key on a row about to be inserted, so the store generates one. Only signed numeric
    /// keys can carry a temp value, so string and Guid keys are deliberately left alone: there, a
    /// client-supplied key is a legitimate choice rather than a placeholder.
    /// </summary>
    private static void ResetTempKey<TRelated, TRelatedKey>(TRelated entity)
        where TRelated : class, IEntity<TRelatedKey>
    {
        // Compare the unwrapped value, not TRelatedKey against default(TRelatedKey): for a nullable key
        // default is null, and Comparer<int?> sorts null FIRST — so Compare(-1, null) is positive and the
        // guard would never fire on the one key shape that most needs it. Boxing a Nullable<int> that has a
        // value yields a boxed int, so the patterns below match it.
        var isTempKey = entity.Id switch
        {
            sbyte v => v < 0,
            short v => v < 0,
            int v => v < 0,
            long v => v < 0,
            decimal v => v < 0,
            float v => v < 0,
            double v => v < 0,
            _ => false,
        };
        if (isTempKey)
        {
            entity.Id = default!;
        }
    }
    #endregion
}