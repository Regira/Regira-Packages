#if NETCOREAPP3_1_OR_GREATER

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.EFcore.Utilities;

namespace Regira.Entities.EFcore.Primers;

/// <summary>
/// Runs the registered primers on every save — <c>SaveChanges()</c> and <c>SaveChangesAsync()</c> alike. On the
/// synchronous call the primers are waited on without the caller's synchronization context, so a primer does not
/// deadlock it on its own awaits.
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

    private Task ApplyPrimersAsync(DbContext context, CancellationToken cancellationToken)
    {
        // Same discovery as the EntityPrimerContainer path (ApplyPrimers): registration-identity
        // dedupe + typed-only registrations included. UseEntities() registers the IServiceCollection;
        // without it (bare setups) fall back to the untyped-services-only legacy resolution.
        var serviceCollection = serviceProvider.GetService<IServiceCollection>();
        var primers = serviceCollection != null
            ? PrimerDiscovery.GetPrimers(serviceProvider, serviceCollection)
            : serviceProvider.GetServices<IEntityPrimer>().Distinct().ToArray();

        return PrimerPass.Run(context, primers, null, logger, cancellationToken);
    }
}

#endif
