using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using System.Collections.Concurrent;

namespace Regira.Entities.DependencyInjection.ServiceCollections;

/// <summary>
/// Records, per <c>UseEntities&lt;TContext&gt;()</c> registration, which DbContext plumbing to wire
/// (<see cref="DbContextWiring"/>). Consulted at options-build time by
/// <see cref="EntityDbContextOptionsConfiguration{TContext}"/> using an assignability check, so the wiring
/// also reaches contexts registered through an abstract base
/// (<c>UseEntities&lt;AppContextBase&gt;()</c> + <c>AddDbContext&lt;SqlServerAppContext&gt;()</c>),
/// regardless of registration order.
/// </summary>
public class DbContextWiringRegistry
{
    private readonly ConcurrentDictionary<Type, DbContextWiring> _wiringByContextType = new();

    /// <summary>
    /// Contributes wiring for a registered context type. Contributions are additive (union per type):
    /// a registration that wires nothing (<see cref="DbContextWiring.None"/> — e.g. a bare
    /// <c>UseEntities&lt;TContext&gt;()</c> without <c>UseDefaults()</c>) contributes nothing and can never
    /// silently disable wiring selected by an earlier registration.
    /// </summary>
    /// <param name="contextType"></param>
    /// <param name="wiring"></param>
    public void Add(Type contextType, DbContextWiring wiring)
    {
        if (wiring == DbContextWiring.None)
        {
            return;
        }
        _wiringByContextType.AddOrUpdate(contextType, wiring, (_, existing) => existing | wiring);
    }

    /// <summary>
    /// The combined wiring for a concrete context type: the union of every registered entry the
    /// type is assignable to (its own registration, base classes, ...).
    /// </summary>
    /// <param name="concreteContextType"></param>
    public DbContextWiring GetFor(Type concreteContextType)
    {
        var wiring = DbContextWiring.None;
        foreach (var entry in _wiringByContextType)
        {
            if (entry.Key.IsAssignableFrom(concreteContextType))
            {
                wiring |= entry.Value;
            }
        }
        return wiring;
    }

    /// <summary>
    /// Gets (or registers) the singleton registry instance for this service collection.
    /// </summary>
    /// <param name="services"></param>
    public static DbContextWiringRegistry For(IServiceCollection services)
    {
        var existing = services.FirstOrDefault(d => d.ServiceType == typeof(DbContextWiringRegistry))?.ImplementationInstance as DbContextWiringRegistry;
        if (existing != null)
        {
            return existing;
        }

        var registry = new DbContextWiringRegistry();
        services.AddSingleton(registry);
        return registry;
    }
}
