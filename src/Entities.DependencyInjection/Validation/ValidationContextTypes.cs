using Microsoft.EntityFrameworkCore;

namespace Regira.Entities.DependencyInjection.Validation;

internal static class ValidationContextTypes
{
    /// <summary>
    /// The concrete <see cref="DbContext"/> types a model-inspecting validator should look at. A recorded
    /// context type may be an abstract base (<c>UseEntities&lt;AppContextBase&gt;()</c> +
    /// <c>AddDbContext&lt;SqlServerAppContext&gt;()</c>); the model EF actually builds belongs to the
    /// registered concrete type(s) it covers.
    /// </summary>
    public static IEnumerable<Type> Inspectable(EntityValidationContext context)
        => context.Registrations.ContextTypes
            .SelectMany(contextType => context.Services
                .Select(d => d.ServiceType)
                .Where(t => !t.IsAbstract && typeof(DbContext).IsAssignableFrom(t) && contextType.IsAssignableFrom(t))
                .DefaultIfEmpty(contextType))
            .Where(t => !t.IsAbstract)
            .Distinct();
}
