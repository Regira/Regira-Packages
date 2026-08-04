using Microsoft.Extensions.DependencyInjection;
using Regira.Utilities;

namespace Regira.DAL.EFcore.Extensions;

public static class ServiceCollectionExtensions
{
    public static IEnumerable<ServiceDescriptor> CollectDescriptors<TService>(this IServiceCollection services)
        => services.Where(s => TypeUtility.ImplementsInterface<TService>(s.ServiceType));
}   