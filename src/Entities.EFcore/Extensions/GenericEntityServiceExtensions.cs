using Regira.Utilities;

namespace Regira.Entities.EFcore.Extensions;

public static class GenericEntityServiceExtensions
{
    public static IList<TService> FindMatchingServices<T, TService>(this IEnumerable<TService> services, T item)
        where TService : class
        where T : class
        => services.FindMatchingServices<T, TService>();
    
    public static IList<TService> FindMatchingServices<T, TService>(this IEnumerable<TService> services)
        where TService : class
        where T : class
        => services.FindMatchingServices(typeof(T));
    
    public static IList<TService> FindMatchingServices<TService>(this IEnumerable<TService> services, Type itemType)
        where TService : class
        => services
            .Distinct()
            // Assume the first generic argument is the entity type
            .Where(service => service.IsMatch(itemType))
            .ToArray();

    public static bool IsMatch<TService>(this TService service, Type itemType)
        => service!.GetType().GetInterfaces()
            .Any(x => x.GenericTypeArguments.Any() && TypeUtility.ImplementsType(itemType, x.GetGenericArguments().First()));
}