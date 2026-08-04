using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// Sets <see cref="IHasLastModified.LastModified"/> to the current time when the entity is modified.<br />
/// With <see cref="DateTimeDefaults.UseUtc"/> enabled (default) values are UTC and a client-supplied
/// value on new entities is normalized to <see cref="DateTimeKind.Utc"/>. When disabled, local time is used
/// and values are kept as given.
/// </summary>
public class HasLastModifiedDbPrimer : EntityPrimerBase<IHasLastModified>
{
    public override Task PrepareAsync(IHasLastModified entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State == EntityState.Modified)
        {
            entity.LastModified = DateTimeDefaults.UseUtc ? DateTime.UtcNow : DateTime.Now;
        }
        else if (DateTimeDefaults.UseUtc && entity.LastModified.HasValue)
        {
            entity.LastModified = entity.LastModified.AsUtc();
        }

        return Task.CompletedTask;
    }
}
