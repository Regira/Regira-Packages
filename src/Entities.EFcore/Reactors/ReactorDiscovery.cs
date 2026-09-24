using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Utilities;
using Regira.Entities.Reactors.Abstractions;
using System.Collections.Concurrent;
using System.Runtime.CompilerServices;

namespace Regira.Entities.EFcore.Reactors;

/// <summary>
/// Finds the registered reactors (<see cref="HookDiscovery"/> rules, registration order) and the entity types each
/// reacts to. A reactor reacts to what its <b>registration</b> names: the <c>TEntity</c> of the
/// <c>IEntityReactor&lt;TEntity&gt;</c> it was registered as — <c>e.AddReactor&lt;T&gt;()</c> names the builder's
/// entity, even for a reactor written against an interface — or, registered untyped
/// (<c>options.AddReactor&lt;T&gt;()</c>), every entity its own <c>IEntityReactor&lt;TEntity&gt;</c> covers. The capture
/// side reads the same from the registrations alone, without instantiating a reactor.
/// </summary>
internal static class ReactorDiscovery
{
    private static readonly ConditionalWeakTable<IServiceCollection, ReactorTargets> TargetsByServices = new();
    private static readonly ConcurrentDictionary<Type, Type[]> DeclaredTargetsByReactorType = new();

    public static RegisteredReactor[] GetReactors(IServiceProvider serviceProvider, IServiceCollection services)
    {
        // one reactor per implementation (HookDiscovery's identity rule); registered more than once, it reacts to
        // everything its registrations name
        var registrationsByIdentity = new Dictionary<object, List<ServiceDescriptor>>();
        var order = new List<object>();
        foreach (var descriptor in HookDiscovery.GetHookRegistrations<IEntityReactor>(services, typeof(IEntityReactor<>)))
        {
            var identity = HookDiscovery.IdentityOf(descriptor);
            if (!registrationsByIdentity.TryGetValue(identity, out var registrations))
            {
                registrationsByIdentity[identity] = registrations = [];
                order.Add(identity);
            }
            registrations.Add(descriptor);
        }

        var result = new List<RegisteredReactor>();
        foreach (var identity in order)
        {
            var registrations = registrationsByIdentity[identity];
            if (HookDiscovery.Materialize<IEntityReactor>(serviceProvider, registrations[0]) is { } reactor)
            {
                result.Add(new RegisteredReactor(reactor, TargetsOf(registrations, reactor.GetType())));
            }
        }
        return [.. result];
    }

    /// <summary>Reactors resolved without their registrations (no <see cref="IServiceCollection"/>): each reacts to what it declares.</summary>
    public static RegisteredReactor[] Unregistered(IEnumerable<IEntityReactor> reactors)
        => reactors.Select(reactor => new RegisteredReactor(reactor, EveryIfNone(DeclaredTargets(reactor.GetType())))).ToArray();

    public static ReactorTargets GetTargets(IServiceCollection services)
    {
        // registrations only grow while the app is being configured — a count change invalidates the cache
        if (TargetsByServices.TryGetValue(services, out var cached) && cached.RegistrationCount == services.Count)
        {
            return cached;
        }
        var targets = BuildTargets(services);
        TargetsByServices.AddOrUpdate(services, targets);
        return targets;
    }

    public static ReactorTargets GetTargets(IEnumerable<IEntityReactor> reactors)
    {
        var all = false;
        var types = new List<Type>();
        foreach (var reactor in reactors)
        {
            var declared = DeclaredTargets(reactor.GetType());
            all |= declared.Length == 0;
            types.AddRange(declared);
        }
        return new ReactorTargets(all, [.. types.Distinct()], -1);
    }

    private static ReactorTargets BuildTargets(IServiceCollection services)
    {
        var all = false;
        var types = new List<Type>();
        foreach (var descriptor in HookDiscovery.GetHookRegistrations<IEntityReactor>(services, typeof(IEntityReactor<>)))
        {
            // registered untyped through a factory, the reactor's type is unknown until it exists: capture everything
            var named = NamedTargets(descriptor, descriptor.ImplementationType ?? descriptor.ImplementationInstance?.GetType());
            all |= named.Length == 0;
            types.AddRange(named);
        }
        return new ReactorTargets(all, [.. types.Distinct()], services.Count);
    }

    // the union of what the registrations name; null = every entity
    private static Type[]? TargetsOf(IEnumerable<ServiceDescriptor> registrations, Type reactorType)
    {
        var targets = new List<Type>();
        foreach (var registration in registrations)
        {
            var named = NamedTargets(registration, reactorType);
            if (named.Length == 0)
            {
                return null;
            }
            targets.AddRange(named);
        }
        return [.. targets.Distinct()];
    }

    // what one registration names: the TEntity it was registered as, or — untyped — what the reactor declares
    private static Type[] NamedTargets(ServiceDescriptor registration, Type? reactorType)
        => registration.ServiceType.IsGenericType
            ? [registration.ServiceType.GetGenericArguments()[0]]
            : reactorType != null ? DeclaredTargets(reactorType) : [];

    // the TEntity of every IEntityReactor<TEntity> a reactor implements; none = it reacts to every entity
    private static Type[] DeclaredTargets(Type reactorType)
        => DeclaredTargetsByReactorType.GetOrAdd(reactorType, type => type.GetInterfaces()
            .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityReactor<>))
            .Select(i => i.GetGenericArguments()[0])
            .ToArray());

    private static Type[]? EveryIfNone(Type[] targets) => targets.Length == 0 ? null : targets;
}

/// <summary>A reactor with the entity types its registrations name; <c>null</c> targets reach every entity.</summary>
internal sealed class RegisteredReactor(IEntityReactor reactor, Type[]? targets)
{
    public IEntityReactor Reactor => reactor;

    /// <summary>Whether it reacts to <paramref name="entityType"/> at all — before its <c>CanReact</c>.</summary>
    public bool Handles(Type entityType) => targets == null || targets.Any(t => t.IsAssignableFrom(entityType));
}

/// <summary>The entity types some reactor reacts to; <see cref="All"/> when a reactor reacts to every entity.</summary>
internal sealed class ReactorTargets(bool all, Type[] types, int registrationCount)
{
    private readonly ConcurrentDictionary<Type, bool> _covers = new();

    public bool All => all;
    public int RegistrationCount => registrationCount;
    public bool IsEmpty => !all && types.Length == 0;

    public bool Covers(Type entityType)
        => all || _covers.GetOrAdd(entityType, type => types.Any(t => t.IsAssignableFrom(type)));
}
