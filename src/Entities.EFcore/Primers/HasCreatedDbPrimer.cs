using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// Sets <see cref="IHasCreated.Created"/> to the current time when the entity is added, or keeps the original value when the entity is modified.<br />
/// With <see cref="DateTimeDefaults.UseUtc"/> enabled (default) the resulting value always has <see cref="DateTimeKind.Utc"/>:
/// client-supplied local times are converted, unspecified kinds are assumed UTC. When disabled, local time is used and values are kept as given.
/// </summary>
public class HasCreatedDbPrimer : EntityPrimerBase<IHasCreated>
{
    public override Task PrepareAsync(IHasCreated entity, EntityEntry entry, CancellationToken token = default)
    {
        var original = (DateTime)entry.OriginalValues[nameof(entity.Created)]!;
        entity.Created = DateTimeDefaults.UseUtc ? original.AsUtc() : original;

        if (entity.Created == DateTime.MinValue)
        {
            entity.Created = DateTimeDefaults.UseUtc ? DateTime.UtcNow : DateTime.Now;
        }

        return Task.CompletedTask;
    }
}
