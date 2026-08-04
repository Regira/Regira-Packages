#if NETCOREAPP3_1_OR_GREATER

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;

namespace Regira.Entities.EFcore.Primers;

public class EntityPrimerContainerInterceptor(IServiceProvider serviceProvider, ILogger<EntityPrimerContainerInterceptor>? logger = null) : SaveChangesInterceptor
{
    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result,
        CancellationToken cancellationToken = default)
    {
        if (eventData.Context is not null)
        {
            // Same discovery as the EntityPrimerContainer path (ApplyPrimers): registration-identity
            // dedupe + typed-only registrations included. UseEntities() registers the IServiceCollection;
            // without it (bare setups) fall back to the untyped-services-only legacy resolution.
            var serviceCollection = serviceProvider.GetService<IServiceCollection>();
            var primers = serviceCollection != null
                ? PrimerDiscovery.GetPrimers(serviceProvider, serviceCollection)
                : serviceProvider.GetServices<IEntityPrimer>().Distinct().ToArray();

            var groupedEntries = eventData.Context
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
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }
}

#endif
