using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using System.Runtime.CompilerServices;

namespace Regira.Entities.EFcore.Utilities;

/// <summary>
/// Tracked entries of a context that a part of the write path recorded something about — per <b>tracking</b> of an
/// entity, not per entity. A mark is kept on EF's internal entry, one per tracking: the same instance attached again
/// after a detach or a <c>ChangeTracker.Clear()</c> gets a new one, unmarked. Held weakly on both ends — an entry that
/// is no longer tracked is collected with its mark, and nothing outlives the context.
/// </summary>
internal sealed class EntryMarks
{
    private static readonly object Mark = new();
    private readonly ConditionalWeakTable<DbContext, ConditionalWeakTable<object, object>> _byContext = new();

    public void Add(EntityEntry entry)
        => _byContext.GetValue(entry.Context, _ => new ConditionalWeakTable<object, object>()).AddOrUpdate(TrackingOf(entry), Mark);

    public bool Contains(EntityEntry entry)
        => _byContext.TryGetValue(entry.Context, out var marks) && marks.TryGetValue(TrackingOf(entry), out _);

    public void Clear(DbContext dbContext)
    {
        if (_byContext.TryGetValue(dbContext, out var marks))
        {
            marks.Clear();
        }
    }

    // EF's internal entry: one per tracking of an entity — ChangeTracker.Clear() discards it, raising no event. Setting
    // the state of an EntityEntry held across a detach tracks the same one again, with the originals it had.
#pragma warning disable EF1001
    private static object TrackingOf(EntityEntry entry) => entry.GetInfrastructure();
#pragma warning restore EF1001
}
