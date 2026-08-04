using Microsoft.EntityFrameworkCore;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.Preppers.Abstractions;
using Regira.Entities.Models.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.EFcore.Preppers;

public class RelatedCollectionPrepper<TContext, TEntity, TRelated, TEntityKey, TRelatedKey>(
    TContext dbContext,
    Expression<Func<TEntity, ICollection<TRelated>?>> navigationExpression,
    IEnumerable<IEntityPrepper<TRelated>>? nestedPreppers = null) : EntityPrepperBase<TEntity>
    where TContext : DbContext
    where TEntity : class, IEntity<TEntityKey>
    where TRelated : class, IEntity<TRelatedKey>
{
    private readonly Func<TEntity, ICollection<TRelated>?> _selectorFunc = navigationExpression.Compile();
    private readonly IReadOnlyList<IEntityPrepper<TRelated>> _nestedPreppers = nestedPreppers?.ToList() ?? [];

    public override async Task Prepare(TEntity modified, TEntity? original, CancellationToken token = default)
    {
        if (original != null)
        {
            // Snapshot the incoming collection: the nested-prepper loop below enumerates it after
            // UpdateRelatedCollection has mutated entity states, and EF Core navigation fixup can add
            // deleted items back into a live navigation collection mid-enumeration.
            var modifiedItems = _selectorFunc(modified)?.ToList();
            // The collection must be reconciled against the rows that actually exist for this entity.
            // If the reload didn't materialize the navigation (the entity's Includes config doesn't
            // eager-load it), load it explicitly here so the original always exposes its related rows.
            // Otherwise every incoming row is diffed against an empty set, treated as new, and re-inserted
            // — breaking idempotent saves and violating unique constraints on join tables.
            var originalItems = _selectorFunc(original) ?? await LoadRelatedCollection(original.Id, token);

            dbContext.UpdateRelatedCollection<TEntity, TRelated, TEntityKey, TRelatedKey>(modified, modifiedItems, original, originalItems);

            if (_nestedPreppers.Count > 0 && modifiedItems != null)
            {
                var originalList = originalItems?.ToList();
                foreach (var modifiedItem in modifiedItems)
                {
                    TRelated? originalItem = null;
                    if (modifiedItem.Id != null && !modifiedItem.Id.Equals(default(TRelatedKey)))
                        originalItem = originalList?.FirstOrDefault(o => o.Id != null && o.Id.Equals(modifiedItem.Id));

                    foreach (var nestedPrepper in _nestedPreppers)
                        await nestedPrepper.Prepare(modifiedItem, originalItem, token);
                }
            }
        }
        else
        {
            var modifiedItems = _selectorFunc(modified)?.ToList();
            if (modifiedItems is { Count: > 0 })
            {
                // Parent is new: explicitly track all items as Added (no original to diff against)
                dbContext.UpdateRelatedCollection<TEntity, TRelated, TEntityKey, TRelatedKey>(modified, modifiedItems, modified, []);

                if (_nestedPreppers.Count > 0)
                {
                    foreach (var modifiedItem in modifiedItems)
                    {
                        foreach (var nestedPrepper in _nestedPreppers)
                            await nestedPrepper.Prepare(modifiedItem, null, token);
                    }
                }
            }
        }
    }

    /// <summary>
    /// Loads the entity's related collection directly from the store. Used as a fallback when the
    /// reloaded <c>original</c> did not eager-load the navigation, guaranteeing the diff in
    /// <see cref="Prepare"/> always sees the rows that currently exist.
    /// </summary>
    private Task<ICollection<TRelated>?> LoadRelatedCollection(TEntityKey id, CancellationToken token)
        // A default id has no persisted rows to load; guard against FilterId no-op'ing on it
        // (see FilterId) and returning an arbitrary first entity's collection.
        => id is null || id.Equals(default(TEntityKey))
            ? Task.FromResult<ICollection<TRelated>?>(null)
            : dbContext.Set<TEntity>()
                .AsNoTracking()
                .FilterId<TEntity, TEntityKey>(id)
                .Select(navigationExpression)
                .FirstOrDefaultAsync(token);
}