#if NETCOREAPP3_1_OR_GREATER

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Extensions;
using Regira.Entities.EFcore.Utilities;
using Regira.Entities.Normalizing.Abstractions;

namespace Regira.Entities.EFcore.Normalizing;

/// <summary>
/// Runs the matching entity normalizers on every save — <c>SaveChanges()</c> and <c>SaveChangesAsync()</c> alike. On
/// the synchronous call the normalizers are waited on without the caller's synchronization context, so a normalizer
/// does not deadlock it on its own awaits.
/// </summary>
public class EntityNormalizerContainerInterceptor(IServiceProvider serviceProvider) : SaveChangesInterceptor
{
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        if (eventData.Context is { } context)
        {
            SyncOverAsync.Wait(() => ApplyNormalizersAsync(context, CancellationToken.None));
        }

        return base.SavingChanges(eventData, result);
    }

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        if (eventData.Context is { } context)
        {
            await ApplyNormalizersAsync(context, cancellationToken);
        }

        return await base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private async Task ApplyNormalizersAsync(DbContext context, CancellationToken cancellationToken)
    {
        var normalizers = serviceProvider.GetServices<IEntityNormalizer>()
            .Distinct()
            .ToArray();

        var groupedEntries = context
            .GetPendingEntries()
            .GroupBy(e => e.Entity.GetType())
            .ToArray();

        if (normalizers.Any() && groupedEntries.Any())
        {
            foreach (var entriesGroup in groupedEntries)
            {
                var matchingNormalizers = normalizers.FindMatchingServices(entriesGroup.Key);

                var entities = entriesGroup.Select(e => e.Entity).ToArray();

                var exclusiveNormalizer = matchingNormalizers.FirstOrDefault(x => x.IsExclusive);
                if (exclusiveNormalizer != null)
                {
                    await exclusiveNormalizer.HandleNormalizeMany(entities, cancellationToken);
                }
                else
                {
                    foreach (var normalizer in matchingNormalizers)
                    {
                        await normalizer.HandleNormalizeMany(entities, cancellationToken);
                    }
                }
            }
        }
    }
}


#endif