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
    /// <c>EntityConstraintException</c> → 409 "Conflict", and <c>EntityConcurrencyException</c> → 409
    /// "Concurrency conflict". Order-independent of <c>AddControllers()</c>, and idempotent.
    /// <para>
    /// Called by <see cref="EntityServiceCollectionJsonExtensions.ConfigureDefaultJsonOptions(IServiceCollection, Action{JsonOptions}, Action{Microsoft.AspNetCore.Http.Json.JsonOptions})"/>,
    /// so an app following the setup guide needs no explicit call. Call it directly only in a host that
    /// configures its JSON options itself.
    /// </para>
    /// </summary>
    public static IServiceCollection MapEntityExceptions(this IServiceCollection services)
        => services.Configure<MvcOptions>(o =>
        {
            // repeated UseEntities()/ConfigureDefaultJsonOptions() calls are common; one the app added by type as
            // well does no harm, since the first filter to handle an exception marks it handled
            if (!o.Filters.OfType<EntityExceptionFilter>().Any())
            {
                o.Filters.Add(new EntityExceptionFilter());
            }
        });
}
