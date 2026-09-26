using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Utilities;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// Removes an <see cref="IArchivable"/> without EF's immediate cascade. EF cascades a delete the moment an entity is
/// marked <c>Deleted</c> — each loaded dependent of a cascading relationship is deleted with it, the foreign key of each
/// loaded optional one is nulled, and a loaded one on a required relationship that does not cascade throws — long before
/// the <see cref="ArchivablePrimer"/> turns the delete into a soft delete, which leaves those rows as they are. Marked
/// with the cascade deferred to the save, the delete touches nothing else; the primer, which undoes the cascade of a
/// delete marked any other way, leaves the dependents of this one alone, so one the caller removed in the same save
/// stays removed. Without the primer the delete is a hard one, and EF cascades it during the save instead.
/// </summary>
internal static class ArchivableRemovalExtensions
{
    private static readonly EntryMarks RemovedWithoutCascade = new();

    /// <summary>Runs <paramref name="remove"/> — which marks <paramref name="entity"/> deleted — without the cascade when it is an <see cref="IArchivable"/>.</summary>
    public static void RemoveWithoutCascade(this DbContext dbContext, object entity, Action remove)
    {
        var tracker = dbContext.ChangeTracker;
        if (entity is not IArchivable || tracker.CascadeDeleteTiming != CascadeTiming.Immediate)
        {
            remove();
            return;
        }

        tracker.CascadeDeleteTiming = CascadeTiming.OnSaveChanges;
        try
        {
            remove();
        }
        finally
        {
            tracker.CascadeDeleteTiming = CascadeTiming.Immediate;
        }
        RemovedWithoutCascade.Add(dbContext.Entry(entity));
    }

    public static bool WasRemovedWithoutCascade(this EntityEntry entry) => RemovedWithoutCascade.Contains(entry);

    public static void ForgetRemovalsWithoutCascade(this DbContext dbContext) => RemovedWithoutCascade.Clear(dbContext);
}
