using Microsoft.Extensions.DependencyInjection;
using Regira.Web.ExceptionHandling.Abstractions;

namespace Regira.Web.ExceptionHandling.DependencyInjection;

[Obsolete("Use AddProblemDetails() with UseExceptionHandler(). Will be removed in v7.")]
public static class ServicesCollectionExtensions
{
    public static IServiceCollection AddGlobalExceptionHandling(this IServiceCollection services)
    {
        services.AddTransient<IExceptionHandler, GlobalExceptionHandler>();
        return services;
    }
}