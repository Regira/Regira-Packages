using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Utilities;

namespace Regira.Entities.EFcore.Primers;

public class EntityPrimerContainer
{
    private readonly DbContext _dbContext;
    private readonly IServiceCollection? _services;
    private readonly ICollection<IEntityPrimer>? _primers;
#pragma warning disable REGIRA0001 // compat path for the obsolete IServiceCollection constructor
    private ICollection<IEntityPrimer> Primers => _primers ?? GetPrimers(_services!);
#pragma warning restore REGIRA0001

    public EntityPrimerContainer(DbContext dbContext, IEnumerable<IEntityPrimer> primers)
    {
        _dbContext = dbContext;
        _primers = primers.ToArray();
    }
    [Obsolete("This overload builds a second service provider to resolve primers. Use the IEnumerable<IEntityPrimer> constructor with the application's provider instead (RegisterPrimerContainer does this). Will be removed in v7.", DiagnosticId = "REGIRA0001")]
    public EntityPrimerContainer(DbContext dbContext, IServiceCollection services)
    {
        _dbContext = dbContext;
        _services = services;
    }

    [Obsolete("Builds a second service provider (singletons and scoped services are duplicated). Resolve IEnumerable<IEntityPrimer> from the application's provider instead. Will be removed in v7.", DiagnosticId = "REGIRA0001")]
    public IEntityPrimer[] GetPrimers(IServiceCollection services)
    {
        var serviceProvider = services.BuildServiceProvider();
        return services
            .Where(s => TypeUtility.ImplementsInterface<IEntityPrimer>(s.ServiceType))
            .Select(x => (IEntityPrimer)serviceProvider.GetService(x.ServiceType)!)
            .ToArray();
    }

    public async Task ApplyPrimers(Type? entityType = null, CancellationToken token = default)
    {
        var groupedEntries = _dbContext.GetPendingEntries()
            .GroupBy(e => e.Entity.GetType())
            .Where(g => entityType == null || g.Key == entityType || TypeUtility.GetBaseTypes(g.Key).Contains(entityType));
        foreach (var entriesGroup in groupedEntries)
        {
            var genericPrimerTypes = new[] { entriesGroup.Key }.Concat(TypeUtility.GetBaseTypes(entriesGroup.Key)).Distinct();
            foreach (var genericPrimerType in genericPrimerTypes)
            {
                var primerType = typeof(IEntityPrimer<>).MakeGenericType(genericPrimerType);
                var primers = Primers.Where(x => TypeUtility.ImplementsInterface(x.GetType(), primerType));
                foreach (var primer in primers)
                {
                    await primer.PrepareManyAsync(entriesGroup.ToArray(), token);
                }
            }
        }
    }
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="T">Type of entity</typeparam>
    /// <param name="token">A token to monitor for cancellation requests.</param>
    /// <returns></returns>
    public Task ApplyPrimers<T>(CancellationToken token = default)
        => ApplyPrimers(typeof(T), token);
}

public static class EntityPrimerContainerExtensions
{
    public static IServiceCollection RegisterPrimerContainer<TContext>(this IServiceCollection services)
        where TContext : DbContext
        // One discovery for both save paths: PrimerDiscovery dedupes by REGISTRATION identity, so a
        // dual-registered class primer (AddPrimer<TEntity, TPrimer>() registers typed + untyped) runs
        // once, while distinct e.Prime(...) lambdas — which share the closed EntityPrimer<TEntity>
        // runtime type — all survive. Typed-only registrations are included.
        => services.AddTransient(p => new EntityPrimerContainer(
            p.GetRequiredService<TContext>(),
            PrimerDiscovery.GetPrimers(p, services)));
}