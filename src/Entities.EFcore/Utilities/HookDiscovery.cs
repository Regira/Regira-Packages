using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.EFcore.Utilities;

/// <summary>
/// Materializes the hooks registered <em>as</em> a hook interface — the non-generic interface or a
/// closed generic variant of it (<c>IEntityPrimer&lt;TEntity&gt;</c>, <c>IEntityReactor&lt;TEntity&gt;</c>) — in
/// registration order. The rules are those of <see cref="Primers.PrimerDiscovery"/>, which documents why each exists:
/// a concrete self-registration is not a hook, dual registrations of one implementation dedupe by implementation
/// identity while distinct lambda factories all survive, and each descriptor is materialized from itself rather than
/// positionally against <c>GetServices</c>.
/// </summary>
internal static class HookDiscovery
{
    public static THook[] GetHooks<THook>(IServiceProvider serviceProvider, IEnumerable<ServiceDescriptor> descriptors, Type genericHookType)
        where THook : class
    {
        var seen = new HashSet<object>();
        var result = new List<THook>();
        foreach (var descriptor in descriptors)
        {
            if (!IsHookRegistration<THook>(descriptor, genericHookType))
            {
                continue;
            }

            if (!seen.Add(IdentityOf(descriptor)))
            {
                continue;
            }

            if (Materialize<THook>(serviceProvider, descriptor) is { } hook)
            {
                result.Add(hook);
            }
        }
        return [.. result];
    }

    /// <summary>
    /// What makes two registrations one hook: the implementation instance, factory or type — not the per-transient
    /// instance, so a dual (typed + untyped) registration of one implementation dedupes while distinct lambda factories
    /// all survive.
    /// </summary>
    public static object IdentityOf(ServiceDescriptor descriptor)
        => descriptor.ImplementationInstance
           ?? (object?)descriptor.ImplementationFactory
           ?? descriptor.ImplementationType
           ?? (object)descriptor;

    /// <summary>The descriptors <see cref="GetHooks{THook}"/> materializes, before the identity dedupe.</summary>
    public static IEnumerable<ServiceDescriptor> GetHookRegistrations<THook>(IEnumerable<ServiceDescriptor> descriptors, Type genericHookType)
        => descriptors.Where(d => IsHookRegistration<THook>(d, genericHookType));

    // Registered as the hook interface itself — THook or a closed generic variant of it. Excludes concrete
    // self-registrations (ServiceType is the implementation) and open-generic definitions (can't be
    // materialized without a concrete entity type).
    private static bool IsHookRegistration<THook>(ServiceDescriptor d, Type genericHookType)
    {
        if (d.IsKeyedService || d.ServiceType.IsGenericTypeDefinition)
        {
            return false;
        }
        return d.ServiceType == typeof(THook)
               || (d.ServiceType.IsGenericType && d.ServiceType.GetGenericTypeDefinition() == genericHookType);
    }

    /// <summary>Resolves the instance for exactly this descriptor — independent of <c>GetServices</c> ordering.</summary>
    public static THook? Materialize<THook>(IServiceProvider sp, ServiceDescriptor d)
        where THook : class
    {
        if (d.ImplementationInstance is THook instance)
        {
            return instance;
        }
        if (d.ImplementationFactory is { } factory)
        {
            return factory(sp) as THook;
        }
        return d.ImplementationType is { } type
            ? ActivatorUtilities.CreateInstance(sp, type) as THook
            : null;
    }
}
