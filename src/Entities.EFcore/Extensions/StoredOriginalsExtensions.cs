using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Regira.Entities.EFcore.Reactors;
using Regira.Entities.EFcore.Utilities;
using System.Runtime.CompilerServices;

namespace Regira.Entities.EFcore.Extensions;

/// <summary>
/// Remembers which tracked entries hold the <b>stored</b> row as their original values, so the
/// <see cref="EntityReactorInterceptor"/> reads the row only for the others. An entry's originals are only known to be
/// the stored row when it came from the database: a tracking query loaded it (the interceptor marks those as they are
/// tracked), or the write path loaded the row and set it as the originals (<c>Modify</c>, the <c>Related()</c> sync).
/// Any other entry holds what its writer attached — <c>Update(detached)</c> reports the new values as the originals, and
/// a stub attached by key its empty ones — however its properties are flagged.
/// <para>
/// A mark belongs to one tracking of the entity (<see cref="EntryMarks"/>): the same instance attached again after a
/// detach or a <c>ChangeTracker.Clear()</c> is a new entry, unmarked. Marks are kept only for a context wired with the
/// reactor interceptor, which drops them all when a save completes — successfully or not.
/// </para>
/// </summary>
internal static class StoredOriginalsExtensions
{
    private static readonly EntryMarks Marks = new();
    private static readonly ConditionalWeakTable<IDbContextOptions, object> ReactorWiring = new();

    /// <summary>Marks an entry the write path gave the stored row as its originals.</summary>
    public static void MarkStoredOriginals(this EntityEntry entry)
    {
        if (entry.Context.HasReactorInterceptor())
        {
            Marks.Add(entry);
        }
    }

    /// <summary>For the reactor interceptor itself: an entry a tracking query loaded.</summary>
    internal static void MarkLoaded(EntityEntry entry) => Marks.Add(entry);

    public static bool HasStoredOriginals(this EntityEntry entry) => Marks.Contains(entry);

    public static void ClearStoredOriginals(this DbContext dbContext) => Marks.Clear(dbContext);

    private static bool HasReactorInterceptor(this DbContext dbContext)
        => (bool)ReactorWiring.GetValue(dbContext.GetService<IDbContextOptions>(),
            options => options.FindExtension<CoreOptionsExtension>()?.Interceptors?.Any(i => i is EntityReactorInterceptor) == true);
}
