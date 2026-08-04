using Entities.Testing.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Entities.Testing.Infrastructure.Primers;

public class CategoryPrimer(
    ProductContext dbContext,
    IEntityPrimer<IHasTimestamps> timestampPrimer,
    NormalizingPrimer normalizingPrimer)
    : EntityPrimerBase<Category>
{
    private int _maxSortOrder;


    public override async Task PrepareManyAsync(IList<EntityEntry> entries, CancellationToken token = default)
    {
        _maxSortOrder = await dbContext.Categories.MaxAsync(x => (int?)x.SortOrder) ?? -1;
        await base.PrepareManyAsync(entries, token);
    }
    public override async Task PrepareAsync(Category entity, EntityEntry entry, CancellationToken token = default)
    {
        await timestampPrimer.PrepareAsync(entity, entry, token);
        await normalizingPrimer.PrepareAsync(entity, entry, token);
        if (entry.State == EntityState.Added)
        {
            entity.SortOrder = ++_maxSortOrder;
        }
    }
}