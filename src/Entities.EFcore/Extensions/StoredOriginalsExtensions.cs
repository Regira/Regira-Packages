using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Regira.Entities.EFcore.Reactors;
using System.Runtime.CompilerServices;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// Remembers which tracked entries the write path loaded with the <b>stored</b> row as their original values, so the
/// <see cref="EntityReactorInterceptor"/> can tell them from an entry whose originals are only what its writer
/// attached — <c>Update(detached)</c> reports every property modified with an original equal to the new value.
/// <para>
/// A mark belongs to one tracking of the entity, not to the entity: it is kept on EF's internal entry, which a
/// <c>ChangeTracker.Clear()</c> or a detach discards, so the same instance attached again is not taken for loaded.
/// Marks are kept only for a context wired with the reactor interceptor, which drops them when a save completes —
/// successfully or not — and they never outlive the context.
/// </para>
/// </summary>
internal static class StoredOriginalsExtensions
{
    private static readonly ConditionalWeakTable<DbContext, HashSet<object>> Marked = new();
    private static readonly ConditionalWeakTable<IDbContextOptions, object> ReactorWiring = new();

    public static void MarkStoredOriginals(this EntityEntry entry)
    {
        if (!entry.Context.HasReactorInterceptor())
        {
            return;
        }
        Marked.GetValue(entry.Context, _ => new HashSet<object>(ReferenceEqualityComparer.Instance)).Add(TrackingOf(entry));
    }

    public static bool HasStoredOriginals(this EntityEntry entry)
        => Marked.TryGetValue(entry.Context, out var marked) && marked.Contains(TrackingOf(entry));

    public static void ClearStoredOriginals(this DbContext dbContext)
    {
        if (Marked.TryGetValue(dbContext, out var marked))
        {
            marked.Clear();
        }
    }

    // EF's internal entry: one per tracking of an entity — a re-attached instance gets a new one
#pragma warning disable EF1001
    private static object TrackingOf(EntityEntry entry) => entry.GetInfrastructure();
#pragma warning restore EF1001

    private static bool HasReactorInterceptor(this DbContext dbContext)
        => (bool)ReactorWiring.GetValue(dbContext.GetService<IDbContextOptions>(),
            options => options.FindExtension<CoreOptionsExtension>()?.Interceptors?.Any(i => i is EntityReactorInterceptor) == true);
}
