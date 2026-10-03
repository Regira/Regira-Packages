using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceCollections;

/// <summary>
/// Holds the app's own repository types, set with <c>UseEntities(o =&gt; o.UseRepository(...))</c>, that replace the
/// default <c>EntityRepository</c> for every <c>For&lt;&gt;()</c> registration that names no repository of its own.
/// <para>
/// A type is an open generic matched to a registration by its number of type parameters, mirroring the five
/// <c>EntityRepository</c> shapes: <c>For&lt;TEntity&gt;()</c> takes a 1-parameter type, <c>For&lt;TEntity, TKey&gt;()</c>
/// a 2-parameter one, and so on up to the 5-parameter complex shape. Each is closed over the registration's type
/// arguments and has to implement every repository and entity-service interface the default it replaces implements.
/// A registration whose shape has no type keeps the default <c>EntityRepository</c>.
/// </para>
/// Shared by every <c>UseEntities()</c> call on the same service collection.
/// </summary>
public class EntityRepositoryRegistry
{
    private static readonly Type[] RepositoryInterfaces =
        [typeof(IEntityRepository<>), typeof(IEntityRepository<,>), typeof(IEntityRepository<,,>), typeof(IEntityRepository<,,,>), typeof(IEntityRepository<,,,,>)];
    private static readonly Type[] EntityServiceInterfaces =
        [typeof(IEntityService<>), typeof(IEntityService<,>), typeof(IEntityService<,,>), typeof(IEntityService<,,,>), typeof(IEntityService<,,,,>)];

    private readonly Dictionary<int, Type> _typesByArity = [];
    private readonly List<(Type EntityType, int Arity)> _entitiesOnDefault = [];

    /// <summary>The app's repository types, one per number of type parameters.</summary>
    public IReadOnlyCollection<Type> RepositoryTypes => _typesByArity.Values;
    /// <summary>
    /// The entities that kept the default <c>EntityRepository</c> although repository types were set, because none has
    /// the number of type parameters their <c>For&lt;&gt;()</c> shape needs.
    /// </summary>
    public IReadOnlyList<(Type EntityType, int Arity)> EntitiesOnDefault => _entitiesOnDefault;

    /// <summary>
    /// Sets <paramref name="repositoryType"/> as the repository for every registration with its number of type
    /// parameters, replacing a type set earlier for that shape.
    /// </summary>
    /// <exception cref="ArgumentException">The type is not an open generic, concrete repository of 1 to 5 type parameters.</exception>
    public void Use(Type repositoryType)
    {
        if (!repositoryType.IsGenericTypeDefinition)
        {
            throw new ArgumentException($"{repositoryType.Name} is not an open generic type. Pass it unbound, e.g. typeof(AppRepository<,>), or give a single entity its repository with e.HasRepository<T>().", nameof(repositoryType));
        }
        if (repositoryType.IsAbstract || repositoryType.IsInterface)
        {
            throw new ArgumentException($"{repositoryType.Name} is abstract and cannot be constructed.", nameof(repositoryType));
        }
        var arity = repositoryType.GetGenericArguments().Length;
        if (arity is < 1 or > 5)
        {
            throw new ArgumentException($"{repositoryType.Name} has {arity} type parameters; a repository type has 1 to 5, matching the EntityRepository shape it replaces.", nameof(repositoryType));
        }
        if (!InterfacesOf(repositoryType, RepositoryInterfaces).Any())
        {
            throw new ArgumentException($"{repositoryType.Name} does not implement IEntityRepository. Derive it from the EntityRepository with the same type parameters.", nameof(repositoryType));
        }
        _typesByArity[arity] = repositoryType;
    }

    /// <summary>
    /// The repository serving the registration that <paramref name="defaultRepositoryType"/> (a closed
    /// <c>EntityRepository</c>) would otherwise serve: the app's type of that shape, closed over the same type
    /// arguments, or the default when no type of that shape was set.
    /// </summary>
    /// <exception cref="InvalidOperationException">The app's type cannot be closed over these type arguments, or misses an interface the default implements.</exception>
    public Type Resolve(Type defaultRepositoryType)
    {
        var typeArguments = defaultRepositoryType.GetGenericArguments();
        var entityType = typeArguments[0];
        if (!_typesByArity.TryGetValue(typeArguments.Length, out var repositoryType))
        {
            if (_typesByArity.Count > 0)
            {
                _entitiesOnDefault.Add((entityType, typeArguments.Length));
            }
            return defaultRepositoryType;
        }

        Type closedType;
        try
        {
            closedType = repositoryType.MakeGenericType(typeArguments);
        }
        catch (ArgumentException ex)
        {
            throw new InvalidOperationException(
                $"{repositoryType.Name} cannot serve {entityType.Name}: its type constraints do not admit <{string.Join(", ", typeArguments.Select(t => t.Name))}>. " +
                $"Give {entityType.Name} a repository of its own with e.HasRepository<T>().", ex);
        }

        var missing = ServiceTypesOf(defaultRepositoryType, asRepository: true, asEntityService: true)
            .Where(i => !i.IsAssignableFrom(closedType))
            .ToArray();
        if (missing.Length > 0)
        {
            throw new InvalidOperationException(
                $"{repositoryType.Name} cannot serve {entityType.Name}: it does not implement {string.Join(", ", missing.Select(FriendlyName))}. " +
                $"Derive it from the EntityRepository with the same type parameters ({FriendlyName(defaultRepositoryType.GetGenericTypeDefinition())}).");
        }
        return closedType;
    }

    /// <summary>
    /// The service types a repository is registered under: the repository and/or entity-service interfaces that
    /// <paramref name="defaultRepositoryType"/> implements.
    /// </summary>
    public static IEnumerable<Type> ServiceTypesOf(Type defaultRepositoryType, bool asRepository, bool asEntityService)
    {
        var serviceTypes = Enumerable.Empty<Type>();
        if (asRepository)
        {
            serviceTypes = serviceTypes.Concat(InterfacesOf(defaultRepositoryType, RepositoryInterfaces));
        }
        if (asEntityService)
        {
            serviceTypes = serviceTypes.Concat(InterfacesOf(defaultRepositoryType, EntityServiceInterfaces));
        }
        return serviceTypes;
    }

    /// <summary>Gets (or registers) the singleton registry instance for this service collection.</summary>
    public static EntityRepositoryRegistry For(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(EntityRepositoryRegistry))?.ImplementationInstance is EntityRepositoryRegistry existing)
        {
            return existing;
        }

        var registry = new EntityRepositoryRegistry();
        services.AddSingleton(registry);
        return registry;
    }

    private static IEnumerable<Type> InterfacesOf(Type type, Type[] definitions)
        => type.GetInterfaces().Where(i => i.IsGenericType && definitions.Contains(i.GetGenericTypeDefinition()));

    private static string FriendlyName(Type type)
    {
        var name = type.Name.Split('`')[0];
        if (!type.IsGenericType)
        {
            return name;
        }
        var arguments = type.GetGenericArguments().Select(a => a.IsGenericParameter ? a.Name : FriendlyName(a));
        return $"{name}<{string.Join(", ", arguments)}>";
    }
}
