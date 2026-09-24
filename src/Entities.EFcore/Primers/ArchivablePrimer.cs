using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// Turns the delete of an <see cref="IArchivable"/> into a soft delete: an update that sets
/// <see cref="IArchivable.IsArchived"/> and writes nothing else of the row. A deleted entry may be a stub —
/// <c>Remove(new Order { Id = id })</c> carries nothing but its key — so its other values are not the stored ones and
/// must not reach the database. What the primers after this one stamp on the update (<c>LastModified</c>, a new
/// concurrency token) is still written: a value a primer changes is detected as modified.
/// </summary>
public class ArchivablePrimer : EntityPrimerBase<IArchivable>
{
    public override Task PrepareAsync(IArchivable entity, EntityEntry entry, CancellationToken token = default)
    {
        if (entry.State == EntityState.Deleted)
        {
            // Modified flags every column — unflag them (which also resets each to its original value), then set and flag
            // the one a soft delete writes. The flag stays written even when the row is archived already, so a repeated
            // delete still reports the row it reached.
            entry.State = EntityState.Modified;
            foreach (var property in entry.Properties.Where(p => !p.Metadata.IsKey()))
            {
                property.IsModified = false;
            }
            foreach (var complexProperty in entry.ComplexProperties)
            {
                complexProperty.IsModified = false;
            }
            entity.IsArchived = true;
            entry.Property(nameof(IArchivable.IsArchived)).IsModified = true;
        }
        return Task.CompletedTask;
    }

    /// <summary>
    /// Whether <paramref name="entry"/> is a soft delete in the making: an update archiving an <see cref="IArchivable"/>
    /// whose original is not archived — what this primer makes of a delete. Like a delete, it may come from a stub, so its
    /// original values need not be the stored ones.
    /// </summary>
    internal static bool IsBeingArchived(EntityEntry entry)
        => entry.State == EntityState.Modified
           && entry.Entity is IArchivable { IsArchived: true }
           && entry.Property(nameof(IArchivable.IsArchived)).OriginalValue is false;
}
