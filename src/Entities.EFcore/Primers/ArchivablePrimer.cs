using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using System.Collections;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// Turns the delete of an <see cref="IArchivable"/> into a soft delete: an update that sets
/// <see cref="IArchivable.IsArchived"/> and writes nothing else of the row. A deleted entry may be a stub —
/// <c>Remove(new Order { Id = id })</c> carries nothing but its key — so its other values are not the stored ones and
/// must not reach the database. What the primers after this one stamp on the update (<c>LastModified</c>, a new
/// concurrency token) is still written: a value a primer changes is detected as modified.
/// <para>
/// The row still exists, so a soft delete leaves the rows that depend on it as they are — archivable or not, loaded
/// or not. EF has already cascaded the delete to the loaded ones by the time this primer runs (see
/// <see cref="UndoCascades"/>), and the primer pass undoes that first. The pass recognizes this primer by its type, a
/// subclass included: a primer that wraps it in a decorator is not recognized, and EF's cascade then deletes the
/// loaded dependents of a row this primer only archives. Derive from it rather than wrap it.
/// </para>
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
    /// Undoes what EF's immediate cascade did to the rows that depend on the soft deletes of this save. When
    /// <c>Remove(parent)</c> marks the parent deleted, EF deletes each loaded dependent of a cascading relationship
    /// (and detaches an added one), and nulls the foreign key of each loaded optional one. Those rows are put back as
    /// they were, their own cascaded dependents with them: a deleted one is tracked as unchanged again (with any
    /// pending edits of its own), an added one as added, and a nulled foreign key is reset to the parent's key. A
    /// dependent that is itself <see cref="IArchivable"/> is restored too, not archived: the first delete of a pass
    /// undoes the cascades of all of them, before any is converted, whatever order the entries come in.
    /// <para>
    /// The cascade leaves no trace of its own, so a dependent the caller removed or detached from the parent in the
    /// same save cannot be told from one EF cascaded, and is put back as well. A delete through the entities write path
    /// cascades nothing to undo (<see cref="ArchivableRemovalExtensions"/>), so there a dependent the caller removed
    /// stays removed. Nothing is undone on a context that does not cascade immediately.
    /// </para>
    /// <para>
    /// The primer pass calls it once, before this primer converts the first delete of the pass.
    /// </para>
    /// </summary>
    internal static void UndoCascades(DbContext dbContext)
    {
        if (dbContext.ChangeTracker.CascadeDeleteTiming != CascadeTiming.Immediate)
        {
            return;
        }

        var pending = dbContext.GetPendingEntries().ToArray();
        var restored = new HashSet<object>(ReferenceEqualityComparer.Instance);
        foreach (var softDelete in pending.Where(e => e is { State: EntityState.Deleted, Entity: IArchivable } && !e.WasRemovedWithoutCascade()))
        {
            if (restored.Add(softDelete.Entity))
            {
                UndoCascade(softDelete, pending, restored);
            }
        }
    }

    private static void UndoCascade(EntityEntry principal, EntityEntry[] pending, HashSet<object> restored)
    {
        foreach (var foreignKey in principal.Metadata.GetReferencingForeignKeys())
        {
            var key = foreignKey.PrincipalKey.Properties.Select(p => principal.Property(p).CurrentValue).ToArray();
            var cascades = foreignKey.DeleteBehavior is DeleteBehavior.Cascade or DeleteBehavior.ClientCascade;
            foreach (var dependent in pending.Where(d => foreignKey.DeclaringEntityType.IsAssignableFrom(d.Metadata)))
            {
                var properties = foreignKey.Properties.Select(dependent.Property).ToArray();
                if (cascades && dependent.State == EntityState.Deleted && properties.Select(p => p.CurrentValue).SequenceEqual(key))
                {
                    dependent.State = EntityState.Unchanged;
                    if (restored.Add(dependent.Entity))
                    {
                        UndoCascade(dependent, pending, restored);
                    }
                }
                else if (!cascades && dependent.State == EntityState.Modified
                         && properties.All(p => p.IsModified && p.CurrentValue == null)
                         && properties.Select(p => p.OriginalValue).SequenceEqual(key))
                {
                    // unflagging resets each to its original value: the parent's key
                    foreach (var property in properties)
                    {
                        property.IsModified = false;
                    }
                }
            }

            // an added dependent the cascade detached is still in the parent's navigation
            if (cascades && foreignKey.PrincipalToDependent is { } navigation)
            {
                foreach (var target in Targets(principal.Navigation(navigation.Name).CurrentValue, navigation.IsCollection))
                {
                    var dependent = principal.Context.Entry(target);
                    if (dependent.State == EntityState.Detached && !dependent.IsKeySet)
                    {
                        dependent.State = EntityState.Added;
                        if (restored.Add(target))
                        {
                            UndoCascade(dependent, pending, restored);
                        }
                    }
                }
            }
        }
    }

    private static IEnumerable<object> Targets(object? value, bool isCollection)
        => value switch
        {
            null => [],
            IEnumerable collection when isCollection => collection.Cast<object>().ToArray(),
            _ => [value]
        };

    /// <summary>
    /// Ends a completed primer pass: the deletes the write path marked without a cascade are converted. A pass broken
    /// off keeps the marks for the save's retry.
    /// </summary>
    internal static void EndPass(DbContext dbContext) => dbContext.ForgetRemovalsWithoutCascade();
}
