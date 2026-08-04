#if NETCOREAPP3_1_OR_GREATER
using Microsoft.AspNetCore.Mvc;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using HttpJsonOptions = Microsoft.AspNetCore.Http.Json.JsonOptions;

namespace Regira.Entities.Web.DependencyInjection;

public static class EntityServiceCollectionOptionsJsonExtensions
{
    /// <inheritdoc cref="EntityServiceCollectionJsonExtensions.ConfigureDefaultJsonOptions(Microsoft.Extensions.DependencyInjection.IServiceCollection,System.Action{JsonOptions},System.Action{HttpJsonOptions})"/>
    /// <param name="options"></param>
    /// <param name="configure">Customizes the MVC JSON options.</param>
    /// <param name="configureHttp">Customizes the minimal-API / OpenAPI JSON options.</param>
    /// <returns></returns>
    public static EntityServiceCollectionOptions ConfigureDefaultJsonOptions(this EntityServiceCollectionOptions options,
        Action<JsonOptions>? configure = null, Action<HttpJsonOptions>? configureHttp = null)
    {
        options.Services
            .ConfigureDefaultJsonOptions(configure, configureHttp);

        return options;
    }
}
#endif
