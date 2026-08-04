using Microsoft.Extensions.DependencyInjection;
using Regira.DAL.EFcore.Extensions;
using Regira.Entities.Normalizing.Abstractions;

namespace Regira.Entities.EFcore.Normalizing;

public static class EntityNormalizerContainerExtensions
{
    public static void AddObjectNormalizingContainer(this IServiceCollection services)
        => services.AddObjectNormalizingContainer((_, o) => o.ExtractFromServiceCollection(services));

    public static IServiceCollection AddObjectNormalizingContainer(this IServiceCollection services, Action<IServiceProvider, EntityNormalizerContainer> configure)
        => services.AddTransient(p =>
        {
            var container = new EntityNormalizerContainer(p);
            configure(p, container);
            return container;
        });

    public static void ExtractFromServiceCollection(this EntityNormalizerContainer container, IServiceCollection services)
    {
        var normalizers = services.CollectDescriptors<IEntityNormalizer>();
        foreach (var descriptor in normalizers)
        {
            var entityType = descriptor.ServiceType.GetGenericArguments().FirstOrDefault() ?? typeof(object);
            container.Register(entityType, p => (IEntityNormalizer)p.GetRequiredService(descriptor.ServiceType));
        }
    }
}