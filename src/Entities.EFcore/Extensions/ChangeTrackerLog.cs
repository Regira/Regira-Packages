using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// Records what a <see cref="DbContext"/> starts tracking, and every state a tracked entity leaves, from creation until
/// <see cref="Dispose"/> — so a write the validators refuse can take back what its preppers marked (a <c>Related()</c>
/// sync marks child rows, and fixup through their back-reference pulls the parent in). It listens to the tracker's
/// events rather than snapshotting it, so its cost follows the write, not the size of the tracker.
/// </summary>
internal sealed class ChangeTrackerLog : IDisposable
{
    private readonly DbContext _dbContext;
    private readonly List<EntityEntry> _tracked = [];
    private readonly HashSet<object> _trackedEntities = new(ReferenceEqualityComparer.Instance);
    private readonly Dictionary<object, (EntityEntry Entry, EntityState State)> _stateBefore = new(ReferenceEqualityComparer.Instance);
    private bool _listening;

    public ChangeTrackerLog(DbContext dbContext)
    {
        _dbContext = dbContext;
        _dbContext.ChangeTracker.Tracked += OnTracked;
        _dbContext.ChangeTracker.StateChanged += OnStateChanged;
        _listening = true;
    }

    private void OnTracked(object? sender, EntityTrackedEventArgs e)
    {
        if (_trackedEntities.Add(e.Entry.Entity))
        {
            _tracked.Add(e.Entry);
        }
    }

    private void OnStateChanged(object? sender, EntityStateChangedEventArgs e)
    {
        // an entity tracked before the log began: keep the first state it left, the one to return to
        if (!_trackedEntities.Contains(e.Entry.Entity))
        {
            _stateBefore.TryAdd(e.Entry.Entity, (e.Entry, e.OldState));
        }
    }

    /// <summary>
    /// Stops tracking every entity the log saw tracked and <paramref name="item"/>, the refused write's own entity, and
    /// returns every other entity tracked before the log to the state it had. The item leaves the tracker even when the
    /// caller tracked it earlier: its values are what the write was refused for, whoever set them.
    /// </summary>
    public void Undo(object item)
    {
        Dispose();
        foreach (var entry in _tracked)
        {
            entry.State = EntityState.Detached;
        }
        foreach (var (entity, (entry, state)) in _stateBefore)
        {
            if (!ReferenceEquals(entity, item) && entry.State != state)
            {
                entry.State = state;
            }
        }
        // Entry() detects the item's changes first, and a required relationship the caller severed on it would throw
        // there, in place of the refusal
        var changeTracker = _dbContext.ChangeTracker;
        var autoDetect = changeTracker.AutoDetectChangesEnabled;
        changeTracker.AutoDetectChangesEnabled = false;
        try
        {
            var own = _dbContext.Entry(item);
            if (own.State != EntityState.Detached)
            {
                own.State = EntityState.Detached;
            }
        }
        finally
        {
            changeTracker.AutoDetectChangesEnabled = autoDetect;
        }
    }

    public void Dispose()
    {
        if (!_listening)
        {
            return;
        }
        _listening = false;
        _dbContext.ChangeTracker.Tracked -= OnTracked;
        _dbContext.ChangeTracker.StateChanged -= OnStateChanged;
    }
}
