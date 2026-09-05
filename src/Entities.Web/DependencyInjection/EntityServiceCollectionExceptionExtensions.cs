using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Web.Controllers;

namespace Regira.Entities.Web.DependencyInjection;

public static class EntityServiceCollectionExceptionExtensions
{
    /// <summary>
    /// Adds <see cref="EntityExceptionFilter"/> to the MVC filter pipeline, so a hand-written action returns
    /// the same statuses the generated ones do: <c>EntityInputException</c> → 400 with its field errors,
    /// <c>EntityConstraintException</c> → 409. Order-independent of <c>AddControllers()</c>, and idempotent
    /// however the filter reached <c>MvcOptions</c> — a second copy would add every model error twice.
    /// <para>
    /// Called by <see cref="EntityServiceCollectionJsonExtensions.ConfigureDefaultJsonOptions(IServiceCollection, Action{JsonOptions}, Action{Microsoft.AspNetCore.Http.Json.JsonOptions})"/>,
    /// so an app following the setup guide needs no explicit call. Call it directly only in a host that
    /// configures its JSON options itself.
    /// </para>
    /// </summary>
    public static IServiceCollection MapEntityExceptions(this IServiceCollection services)
        => services.Configure<MvcOptions>(o =>
        {
            // The filter is stateless, so it registers as an instance. Repeated
            // UseEntities()/ConfigureDefaultJsonOptions() calls are common, and a consumer may also have
            // added it by type — `Filters.Add<EntityExceptionFilter>()` is a TypeFilterAttribute, not an
            // instance of it, so an `is` test alone would not see that one.
            if (o.Filters.Any(AlreadyRegistered))
                return;
            o.Filters.Add(new EntityExceptionFilter());
        });

    private static bool AlreadyRegistered(IFilterMetadata filter) => filter switch
    {
        EntityExceptionFilter => true,
        TypeFilterAttribute f => f.ImplementationType == typeof(EntityExceptionFilter),
        ServiceFilterAttribute f => f.ServiceType == typeof(EntityExceptionFilter),
        _ => false,
    };
}
