using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Regira.Entities.Mediator.Abstractions;

namespace Regira.Entities.Mediator.DependencyInjection;

public static class EntityMediatorServiceCollectionExtensions
{
    /// <summary>
    /// Registers the in-house <see cref="IEntitySender"/>, the <see cref="IEntityRequestExecutor"/> and the
    /// <see cref="IEntityInputValidator"/> — MVC's model validation where Regira.Entities.Web is present, DataAnnotations
    /// otherwise. <c>UseEntities()</c> calls it, so an app needs no explicit call; each registration is a <c>TryAdd</c>,
    /// so an adapter that replaced the sender keeps it whatever the order.
    /// </summary>
    public static IServiceCollection AddEntityMediator(this IServiceCollection services)
    {
        services.TryAddScoped<IEntityRequestExecutor, EntityRequestExecutor>();
        services.TryAddScoped<IEntitySender, EntitySender>();

        // Regira.Entities.Web references this package, so its MVC validator is bound late: a patch is then judged by
        // the rules a request body is, implicit [Required] on non-nullable references and the app's validator providers included
        var mvcInputValidator = Type.GetType("Regira.Entities.Web.Controllers.MvcEntityInputValidator, Regira.Entities.Web", throwOnError: false);
        if (mvcInputValidator != null)
        {
            services.TryAdd(ServiceDescriptor.Scoped(typeof(IEntityInputValidator), mvcInputValidator));
        }
        services.TryAddSingleton<IEntityInputValidator>(DataAnnotationsEntityInputValidator.Instance);
        return services;
    }
}
