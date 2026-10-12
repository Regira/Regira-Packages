using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// The one primer pass behind both save paths, <see cref="EntityPrimerContainer.ApplyPrimers(Type?, CancellationToken)"/>
/// and <see cref="EntityPrimerContainerInterceptor"/>: the pending entries are grouped by type once, each primer runs in
/// registration order over every group it matches, and the end-of-pass bookkeeping runs once.
/// </summary>
internal static class PrimerPass
{
    /// <param name="dbContext">The context whose pending entries are primed.</param>
    /// <param name="primers">The registered primers, in registration order.</param>
    /// <param name="entityType">Only the entries of this type or a subtype; every pending entry when <c>null</c>.</param>
    /// <param name="logger">Logs each primer run at Debug level.</param>
    /// <param name="token">A token to monitor for cancellation requests.</param>
    public static async Task Run(DbContext dbContext, IReadOnlyCollection<IEntityPrimer> primers, Type? entityType, ILogger? logger, CancellationToken token)
    {
        var groups = dbContext.GetPendingEntries()
            .GroupBy(e => e.Entity.GetType())
            .Where(g => entityType == null || entityType.IsAssignableFrom(g.Key))
            .Select(g => (Type: g.Key, Entries: g.ToArray()))
            .ToArray();

        var cascadesUndone = false;
        foreach (var primer in primers)
        {
            foreach (var (type, entries) in groups)
            {
                if (!primer.IsMatch(type))
                {
                    continue;
                }
                if (primer is ArchivablePrimer && !cascadesUndone && entries.Any(e => e.State == EntityState.Deleted && primer.CanPrepare(e.Entity)))
                {
                    // before the first delete of the pass is converted: a cascaded dependent may be one of the deletes
                    ArchivablePrimer.UndoCascades(dbContext);
                    cascadesUndone = true;
                }
                logger?.LogDebug("Priming {Count} {EntityType} entries using {Primer}", entries.Length, type.FullName, primer.GetType().FullName);
                await primer.PrepareManyAsync(entries, token);
            }
        }

        ArchivablePrimer.EndPass(dbContext);
    }
}
