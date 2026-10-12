using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
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

    /// <summary>
    /// Runs the primers on the pending entries without saving, in registration order, as <c>SaveChanges()</c> does
    /// through <see cref="EntityPrimerContainerInterceptor"/>.
    /// </summary>
    /// <param name="entityType">Only the entries of this type or a subtype; every pending entry when <c>null</c>.</param>
    /// <param name="token">A token to monitor for cancellation requests.</param>
    public Task ApplyPrimers(Type? entityType = null, CancellationToken token = default)
        => PrimerPass.Run(_dbContext, Primers.ToArray(), entityType, null, token);
    /// <summary>
    /// Runs the primers on the pending entries of <typeparamref name="T"/> without saving.
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