using Microsoft.Extensions.DependencyInjection;
using Regira.Web.ExceptionHandling.Abstractions;

namespace Regira.Web.ExceptionHandling.DependencyInjection;

public static class ServicesCollectionExtensions
{
    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddTransient<IExceptionHandler, GlobalExceptionHandler>();
        return services;
    }
}