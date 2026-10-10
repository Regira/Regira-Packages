using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Mediator.Abstractions;

namespace Regira.Entities.Mediator;

public static class EntityServiceProviderExtensions
{
    /// <summary>
    /// The registered <see cref="IEntitySender"/> — the in-house one that <c>UseEntities()</c> registers, or an adapter's —
    /// or, in a host wired without <c>UseEntities()</c>, an in-house sender over this provider.
    /// </summary>
    public static IEntitySender GetEntitySender(this IServiceProvider services)
        => services.GetService<IEntitySender>()
           ?? new EntitySender(services.GetService<IEntityRequestExecutor>() ?? new EntityRequestExecutor(services));

    /// <summary>
    /// Resolves an <c>IEntityService&lt;…&gt;</c>, and when it is missing throws with what <c>For&lt;&gt;()</c> did register
    /// for the entity, so a type-argument mismatch between a caller and its registration reads as one.
    /// </summary>
    public static TService GetRequiredEntityService<TService>(this IServiceProvider services)
        where TService : notnull
    {
        try
        {
            return services.GetRequiredService<TService>();
        }
        catch (InvalidOperationException ex)
        {
            var registrations = services.GetService<IServiceCollection>();
            throw new InvalidOperationException(EntityServiceDiagnostics.DescribeMissingService(typeof(TService), registrations), ex);
        }
    }
}
