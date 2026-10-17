using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.DependencyInjection.ServiceCollections;

internal static class ServiceCollectionInstances
{
    /// <summary>
    /// The instance registered as <typeparamref name="T"/>, or a new one registered now: one per service collection,
    /// shared by every call on it — every <c>UseEntities()</c> call and every builder.
    /// </summary>
    public static T GetOrAddInstance<T>(this IServiceCollection services)
        where T : class, new()
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(T))?.ImplementationInstance is T existing)
        {
            return existing;
        }

        var instance = new T();
        services.AddSingleton(instance);
        return instance;
    }
}
