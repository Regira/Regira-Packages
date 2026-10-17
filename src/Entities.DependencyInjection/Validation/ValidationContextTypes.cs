using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>A registered context a model check inspects, as its type and the instance the run resolved.</summary>
internal sealed record ValidatedDbContext(Type Type, DbContext Context);

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

    /// <summary>
    /// Resolves every <see cref="Inspectable"/> context from the run's provider, with its model and options. Building a
    /// model needs no connection, but resolving a context or its model can still throw — a throwing factory, a context
    /// configured with no database provider — so such a context is left out with the reason, which the run reports once.
    /// </summary>
    public static (IReadOnlyList<ValidatedDbContext> Resolved, IReadOnlyList<string> Unresolved) Resolve(EntityValidationContext context)
    {
        var resolved = new List<ValidatedDbContext>();
        var unresolved = new List<string>();
        foreach (var type in Inspectable(context))
        {
            try
            {
                var dbContext = (DbContext)context.Provider.GetRequiredService(type);
                // what the checks read, built here so a context that cannot build them fails here rather than in a check
                _ = dbContext.Model;
                _ = dbContext.GetService<IDbContextOptions>();
                resolved.Add(new ValidatedDbContext(type, dbContext));
            }
            catch (Exception ex)
            {
                unresolved.Add($"Could not resolve {type.Name}, so the startup checks that read its model skipped it: {ex.Message}");
            }
        }
        return (resolved, unresolved);
    }
}
