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
/// synchronous call the primers are waited on without the caller's synchronization context, so a primer that awaits
/// cannot deadlock it.
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

    private async Task ApplyPrimersAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Same discovery as the EntityPrimerContainer path (ApplyPrimers): registration-identity
        // dedupe + typed-only registrations included. UseEntities() registers the IServiceCollection;
        // without it (bare setups) fall back to the untyped-services-only legacy resolution.
        var serviceCollection = serviceProvider.GetService<IServiceCollection>();
        var primers = serviceCollection != null
            ? PrimerDiscovery.GetPrimers(serviceProvider, serviceCollection)
            : serviceProvider.GetServices<IEntityPrimer>().Distinct().ToArray();

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
    }
}

#endif
