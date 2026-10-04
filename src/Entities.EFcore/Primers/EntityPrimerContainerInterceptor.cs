#if NETCOREAPP3_1_OR_GREATER

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.Utilities;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// Runs the registered primers on every save — <c>SaveChanges()</c> and <c>SaveChangesAsync()</c> alike. On the
/// synchronous call the primers are waited on without the caller's synchronization context, so a primer does not
/// deadlock it on its own awaits. What a primer leaves to the end of the save (<see cref="SaveOutcomes"/>) is undone when
/// the save fails, the primer pass itself included, and finished when it succeeds.
/// </summary>
public class EntityPrimerContainerInterceptor(IServiceProvider serviceProvider, ILogger<EntityPrimerContainerInterceptor>? logger = null) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            SyncOverAsync.Wait(() => ApplyPrimersAsync(context, CancellationToken.None));
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await ApplyPrimersAsync(context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        SyncOverAsync.Wait(() => SaveOutcomes.Saved(eventData.Context));
        return base.SavedChanges(eventData, result);
    }
    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        await SaveOutcomes.Saved(eventData.Context);
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        SyncOverAsync.Wait(() => SaveOutcomes.Failed(eventData.Context));
        base.SaveChangesFailed(eventData);
    }
    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        await SaveOutcomes.Failed(eventData.Context);
        await base.SaveChangesFailedAsync(eventData, cancellationToken);
    }
    public override void SaveChangesCanceled(DbContextEventData eventData)
    {
        SyncOverAsync.Wait(() => SaveOutcomes.Failed(eventData.Context));
        base.SaveChangesCanceled(eventData);
    }
    public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default)
    {
        await SaveOutcomes.Failed(eventData.Context);
        await base.SaveChangesCanceledAsync(eventData, cancellationToken);
    }

    // a primer that throws ends the save before EF's own failure hooks: what the pass left for it is undone here
    private async Task ApplyPrimersAsync(DbContext context, CancellationToken cancellationToken)
    {
        await SaveOutcomes.BeginSavePass(context);
        try
        {
            await ApplyPrimersCoreAsync(context, cancellationToken);
        }
        catch
        {
            await SaveOutcomes.Failed(context);
            throw;
        }
        finally
        {
            SaveOutcomes.EndSavePass(context);
        }
    }

    private async Task ApplyPrimersCoreAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Same discovery as the EntityPrimerContainer path (ApplyPrimers): registration-identity
        // dedupe + typed-only registrations included. UseEntities() registers the IServiceCollection;
        // without it (bare setups) fall back to the untyped-services-only legacy resolution.
        var serviceCollection = serviceProvider.GetService<IServiceCollection>();
        var primers = serviceCollection != null
            ? PrimerDiscovery.GetPrimers(serviceProvider, serviceCollection)
            : serviceProvider.GetServices<IEntityPrimer>().Distinct().ToArray();

        ArchivablePrimer.BeginPass(context);
        var groupedEntries = context
            .GetPendingEntries()
            .GroupBy(e => e.Entity.GetType())
            .ToArray();

        if (primers.Any() && groupedEntries.Any())
        {
            // execute primers in same order than they were registered
            foreach (var primer in primers)
            {
                foreach (var entriesGroup in groupedEntries)
                {
                    if (primer.IsMatch(entriesGroup.Key))
                    {
                        logger?.LogDebug($"Priming {entriesGroup.Count()} {entriesGroup.Key.FullName} entries using {primer.GetType().FullName}");
                        await primer.PrepareManyAsync(entriesGroup.ToArray(), cancellationToken);
                    }
                }
            }
        }

        // only now is it known which concurrency tokens a primer moves — those are compared with the client's value
        context.ApplyUndecidedClientTokens();
        ArchivablePrimer.EndPass(context);
    }
}

#endif
