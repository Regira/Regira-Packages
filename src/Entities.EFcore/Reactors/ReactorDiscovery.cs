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
/// (<c>options.AddReactor&lt;T&gt;()</c>), every entity its own <c>IEntityReactor&lt;TEntity&gt;</c> covers. Both are
/// read from the registrations alone, once: a reactor is only instantiated for a change of a type it reacts to.
/// </summary>
internal static class ReactorDiscovery
{
    private static readonly ConditionalWeakTable<IServiceCollection, ReactorCatalog> Catalogs = new();
    private static readonly ConcurrentDictionary<Type, Type[]> DeclaredTargetsByReactorType = new();

    public static ReactorCatalog GetCatalog(IServiceCollection services)
    {
        // registrations only grow while the app is being configured — a count change invalidates the cache
        if (Catalogs.TryGetValue(services, out var cached) && cached.RegistrationCount == services.Count)
        {
            return cached;
        }
        var catalog = BuildCatalog(services);
        Catalogs.AddOrUpdate(services, catalog);
        return catalog;
    }

    public static ReactorTargets GetTargets(IServiceCollection services) => GetCatalog(services).Targets;

    /// <summary>Reactors resolved without their registrations (no <see cref="IServiceCollection"/>): each reacts to what it declares.</summary>
    public static ReactorRegistration[] Resolved(IEnumerable<IEntityReactor> reactors)
        => reactors.Select(reactor => ReactorRegistration.Resolved(reactor, EveryIfNone(DeclaredTargets(reactor.GetType())))).ToArray();

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
        return new ReactorTargets(all, [.. types.Distinct()]);
    }

    private static ReactorCatalog BuildCatalog(IServiceCollection services)
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

        var all = false;
        var types = new List<Type>();
        var reactors = new List<ReactorRegistration>();
        foreach (var registrations in order.Select(identity => registrationsByIdentity[identity]))
        {
            // registered untyped through a factory, what the reactor reacts to is unknown until it exists
            var first = registrations[0];
            var reactorType = first.ImplementationType ?? first.ImplementationInstance?.GetType();
            var known = reactorType != null || registrations.All(r => r.ServiceType.IsGenericType);
            var targets = known ? TargetsOf(registrations, reactorType) : null;
            reactors.Add(ReactorRegistration.Registered(registrations, known, targets));
            all |= targets == null;
            types.AddRange(targets ?? []);
        }
        return new ReactorCatalog([.. reactors], new ReactorTargets(all, [.. types.Distinct()]), services.Count);
    }

    /// <summary>The union of what the registrations name; <c>null</c> = every entity.</summary>
    internal static Type[]? TargetsOf(IEnumerable<ServiceDescriptor> registrations, Type? reactorType)
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

    internal static bool Covers(Type[]? targets, Type entityType) => targets == null || targets.Any(t => t.IsAssignableFrom(entityType));
}

/// <summary>The registered reactors in registration order, and the entity types they react to together.</summary>
internal sealed record ReactorCatalog(ReactorRegistration[] Registrations, ReactorTargets Targets, int RegistrationCount);

/// <summary>
/// One reactor as registered — an implementation, registered once or more — with the entity types its registrations
/// name, known before it is instantiated unless it is registered untyped through a factory.
/// </summary>
internal sealed class ReactorRegistration
{
    private readonly IReadOnlyList<ServiceDescriptor> _registrations;
    private readonly IEntityReactor? _resolved;
    private readonly bool _targetsKnown;
    private readonly Type[]? _targets;

    private ReactorRegistration(IReadOnlyList<ServiceDescriptor> registrations, IEntityReactor? resolved, bool targetsKnown, Type[]? targets)
    {
        _registrations = registrations;
        _resolved = resolved;
        _targetsKnown = targetsKnown;
        _targets = targets;
    }

    public static ReactorRegistration Registered(IReadOnlyList<ServiceDescriptor> registrations, bool targetsKnown, Type[]? targets)
        => new(registrations, null, targetsKnown, targets);

    public static ReactorRegistration Resolved(IEntityReactor reactor, Type[]? targets)
        => new([], reactor, true, targets);

    /// <summary>The reactor's type, as far as it is known before it exists.</summary>
    public string Name
        => (_resolved?.GetType() ?? _registrations[0].ImplementationType ?? _registrations[0].ImplementationInstance?.GetType())?.FullName
           ?? _registrations[0].ServiceType.FullName!;

    /// <summary>Whether it may react to <paramref name="entityType"/> — decided without instantiating it when its targets are known.</summary>
    public bool MayHandle(Type entityType) => !_targetsKnown || ReactorDiscovery.Covers(_targets, entityType);

    public RegisteredReactor? Materialize(IServiceProvider scopedProvider)
    {
        var reactor = _resolved ?? HookDiscovery.Materialize<IEntityReactor>(scopedProvider, _registrations[0]);
        return reactor == null
            ? null
            : new RegisteredReactor(reactor, _targetsKnown ? _targets : ReactorDiscovery.TargetsOf(_registrations, reactor.GetType()));
    }
}

/// <summary>A reactor with the entity types its registrations name; <c>null</c> targets reach every entity.</summary>
internal sealed class RegisteredReactor(IEntityReactor reactor, Type[]? targets)
{
    public IEntityReactor Reactor => reactor;

    /// <summary>Whether it reacts to <paramref name="entityType"/> at all — before its <c>CanReact</c>.</summary>
    public bool Handles(Type entityType) => ReactorDiscovery.Covers(targets, entityType);
}

/// <summary>The entity types some reactor reacts to; <see cref="All"/> when a reactor reacts to every entity.</summary>
internal sealed class ReactorTargets(bool all, Type[] types)
{
    private readonly ConcurrentDictionary<Type, bool> _covers = new();

    public bool All => all;
    public bool IsEmpty => !all && types.Length == 0;

    public bool Covers(Type entityType)
        => all || _covers.GetOrAdd(entityType, type => types.Any(t => t.IsAssignableFrom(type)));
}
