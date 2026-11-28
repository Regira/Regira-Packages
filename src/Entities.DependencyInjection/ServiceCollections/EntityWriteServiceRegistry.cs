using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceCollections;

/// <summary>
/// Holds the app's own write service type, set with <c>UseEntities(o =&gt; o.UseWriteService(...))</c>, that replaces the
/// default <c>EntityWriteService</c> for every <c>For&lt;&gt;()</c> registration that names no write service of its own.
/// <para>
/// The type is an open generic with the type parameters of <c>EntityWriteService&lt;TContext, TEntity, TKey&gt;</c>, closed
/// over the registration's context, entity and key. It serves every <c>For&lt;&gt;()</c> shape: the int-keyed default
/// <c>EntityWriteService&lt;TContext, TEntity&gt;</c> is the same class closed over <see cref="int"/>.
/// </para>
/// Shared by every <c>UseEntities()</c> call on the same service collection.
/// </summary>
public class EntityWriteServiceRegistry
{
    /// <summary>The app's write service type, or <c>null</c> when none was set.</summary>
    public Type? WriteServiceType { get; private set; }

    /// <summary>Sets <paramref name="writeServiceType"/> as the write service of every registration, replacing a type set earlier.</summary>
    /// <exception cref="ArgumentException">The type is not an open generic, concrete write service of 3 type parameters.</exception>
    public void Use(Type writeServiceType)
    {
        if (!writeServiceType.IsGenericTypeDefinition)
        {
            throw new ArgumentException($"{writeServiceType.Name} is not an open generic type. Pass it unbound, e.g. typeof(AppWriteService<,,>), or give a single entity its write service with e.UseWriteService<T>().", nameof(writeServiceType));
        }
        if (writeServiceType.IsAbstract || writeServiceType.IsInterface)
        {
            throw new ArgumentException($"{writeServiceType.Name} is abstract and cannot be constructed.", nameof(writeServiceType));
        }
        var arity = writeServiceType.GetGenericArguments().Length;
        if (arity != 3)
        {
            throw new ArgumentException($"{writeServiceType.Name} has {arity} type parameter{(arity == 1 ? "" : "s")}; a write service type has 3, <TContext, TEntity, TKey>, matching the EntityWriteService it derives from.", nameof(writeServiceType));
        }
        var interfaces = writeServiceType.GetInterfaces().Where(i => i.IsGenericType).Select(i => i.GetGenericTypeDefinition()).ToArray();
        if (!interfaces.Contains(typeof(IEntityWriteService<,>)))
        {
            throw new ArgumentException($"{writeServiceType.Name} does not implement IEntityWriteService. Derive it from EntityWriteService<TContext, TEntity, TKey>.", nameof(writeServiceType));
        }
        if (interfaces.Contains(typeof(IEntityService<,>)))
        {
            throw new ArgumentException($"{writeServiceType.Name} is an entity service, not a write service. Set an app-wide repository with UseRepository().", nameof(writeServiceType));
        }
        WriteServiceType = writeServiceType;
    }

    /// <summary>
    /// The write service serving the registration that <paramref name="defaultWriteServiceType"/> (a closed
    /// <c>EntityWriteService</c>) would otherwise serve: the app's type closed over the same context, entity and key,
    /// or the default when no type was set.
    /// </summary>
    /// <exception cref="InvalidOperationException">The app's type cannot be closed over this context, entity and key, or
    /// does not implement the <c>IEntityWriteService&lt;TEntity, TKey&gt;</c> the default implements.</exception>
    public Type Resolve(Type defaultWriteServiceType)
    {
        if (WriteServiceType == null)
        {
            return defaultWriteServiceType;
        }

        var contextType = defaultWriteServiceType.GetGenericArguments()[0];
        var serviceType = defaultWriteServiceType.GetInterfaces()
            .Single(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEntityWriteService<,>));
        var entityType = serviceType.GetGenericArguments()[0];
        Type[] typeArguments = [contextType, .. serviceType.GetGenericArguments()];

        Type closedType;
        try
        {
            closedType = WriteServiceType.MakeGenericType(typeArguments);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"{WriteServiceType.Name} cannot serve {entityType.Name}: its type constraints do not admit <{string.Join(", ", typeArguments.Select(t => t.Name))}>. " +
                $"Give {entityType.Name} a write service of its own with e.UseWriteService<T>().", ex);
        }

        if (!serviceType.IsAssignableFrom(closedType))
        {
            throw new InvalidOperationException(
                $"{WriteServiceType.Name} cannot serve {entityType.Name}: {EntityRepositoryRegistry.FriendlyName(closedType)} does not implement " +
                $"{EntityRepositoryRegistry.FriendlyName(serviceType)}. Declare its type parameters in the order of EntityWriteService<TContext, TEntity, TKey> and derive from it.");
        }
        return closedType;
    }

    /// <summary>Gets (or registers) the singleton registry instance for this service collection.</summary>
    public static EntityWriteServiceRegistry For(IServiceCollection services) => services.GetOrAddInstance<EntityWriteServiceRegistry>();
}
