using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Normalizing.Abstractions;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Detects primers/normalizers that are registered in DI but can never run because the matching
/// SaveChanges interceptor is missing from the DbContext options — the "prepper is registered but
/// silently does nothing" class of bug. Inspects the context's final options (so interceptors added
/// in <c>OnConfiguring</c> count too).
/// </summary>
internal sealed class InterceptorWiringValidator : IEntityRegistrationValidator
{
    public IEnumerable<EntityValidationIssue> Validate(EntityValidationContext context)
    {
        using var scope = context.Provider.CreateScope();
        var provider = scope.ServiceProvider;

        // Resolving primer/normalizer instances can throw if a factory is misconfigured — a DIAGNOSTIC
        // validator must never turn that into a startup crash, so it is isolated in a non-iterator helper
        // (yield return cannot live inside try/catch) that reports the failure as a Warning instead.
        var (primerTargets, normalizerTargets, resolutionError) = ResolveTargets(provider, context);
        if (resolutionError != null)
        {
            yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                $"Could not resolve primers/normalizers to validate interceptor wiring: {resolutionError}");
            yield break;
        }
        if (primerTargets.Count == 0 && normalizerTargets.Count == 0)
        {
            yield break;
        }

        foreach (var contextType in context.Registrations.ContextTypes)
        {
            var contextEntities = context.Registrations.Entities
                .Where(e => e.ContextType == contextType)
                .Select(e => e.EntityType)
                .ToArray();
            var hasPrimers = Targets(primerTargets, contextEntities);
            var hasNormalizers = Targets(normalizerTargets, contextEntities);
            if (!hasPrimers && !hasNormalizers)
            {
                continue; // this context has no prime-able/normalize-able entities — no interceptor needed
            }
            // The recorded type may be an abstract base (UseEntities<AppContextBase>() +
            // AddDbContext<SqlServerAppContext>()): inspect the registered concrete context type(s) it
            // covers — those are the options EF actually builds.
            var inspectTypes = context.Services
                .Select(d => d.ServiceType)
                .Where(t => !t.IsAbstract && typeof(DbContext).IsAssignableFrom(t) && contextType.IsAssignableFrom(t))
                .Distinct()
                .ToArray();
            if (inspectTypes.Length == 0)
            {
                inspectTypes = [contextType]; // not registered at all — the resolution below reports the failure
            }

            foreach (var inspectType in inspectTypes)
            {
                IInterceptor[]? interceptors = null;
                string? inspectionFailure = null;
                try
                {
                    var dbContext = (DbContext)provider.GetRequiredService(inspectType);
                    var optionsInterceptors = dbContext.GetService<IDbContextOptions>()
                        .Extensions.OfType<CoreOptionsExtension>()
                        .FirstOrDefault()?.Interceptors ?? [];
                    // EF Core also applies IInterceptor services registered in the application provider, so
                    // include those — a user may wire the interceptor via DI rather than AddInterceptors(sp).
                    var diInterceptors = provider.GetServices<IInterceptor>();
                    interceptors = optionsInterceptors.Concat(diInterceptors).ToArray();
                }
                catch (Exception ex)
                {
                    inspectionFailure = ex.Message;
                }
                if (interceptors == null)
                {
                    yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                        $"Could not inspect {inspectType.Name} for interceptor wiring: {inspectionFailure}");
                    continue;
                }

                if (hasPrimers && !interceptors.Any(i => i is EntityPrimerContainerInterceptor))
                {
                    // The documented non-interceptor pattern (RegisterPrimerContainer + explicit ApplyPrimers()
                    // calls) IS detectable — a deliberate configuration, not a bug. An overridden SaveChanges is
                    // not detectable, so the remaining case warns loudly instead of gating startup on a
                    // configuration this validator cannot see.
                    var usesPrimerContainer = context.Services.Any(d => d.ServiceType == typeof(EntityPrimerContainer));
                    yield return usesPrimerContainer
                        ? new EntityValidationIssue(EntityValidationSeverity.Info,
                            $"{inspectType.Name} has no primer interceptor, but an EntityPrimerContainer is registered — primers run only where ApplyPrimers() is called explicitly. " +
                            $"Wire the primer interceptor (UseDefaults(), or WireDbContext(DbContextWiring.PrimerInterceptors)) if SaveChanges should prime too.")
                        : new EntityValidationIssue(EntityValidationSeverity.Warning,
                            $"IEntityPrimer services are registered but {inspectType.Name} has no primer interceptor — primers (timestamps, archiving, Related() preppers) will not run on SaveChanges for this context (unless a custom SaveChanges applies them). " +
                            $"Fix: services.UseEntities<{contextType.Name}>(e => e.UseDefaults()) — or e.WireDbContext(DbContextWiring.PrimerInterceptors) for à-la-carte wiring");
                }
                if (hasNormalizers && !interceptors.Any(i => i is EntityNormalizerContainerInterceptor))
                {
                    yield return new EntityValidationIssue(EntityValidationSeverity.Warning,
                        $"IEntityNormalizer services are registered but {inspectType.Name} has no normalizer interceptor — normalized fields (NormalizedTitle, NormalizedContent, ?q= search data) will not be populated on SaveChanges for this context (unless a custom SaveChanges applies them). " +
                        $"Fix: services.UseEntities<{contextType.Name}>(e => e.UseDefaults()) — or e.WireDbContext(DbContextWiring.NormalizerInterceptors) for à-la-carte wiring");
                }
            }
        }
    }

    /// <summary>
    /// The entity type each resolved primer/normalizer targets, read from its <c>I...&lt;T&gt;</c> interface.
    /// A hook implementing only the non-generic marker targets everything (<c>null</c>).
    /// </summary>
    private static (List<Type?> Primers, List<Type?> Normalizers, string? Error) ResolveTargets(
        IServiceProvider provider, EntityValidationContext context)
    {
        try
        {
            var primerTargets = TargetsOf(provider.GetServices<IEntityPrimer>(), typeof(IEntityPrimer<>));
            var normalizerTargets = TargetsOf(provider.GetServices<IEntityNormalizer>(), typeof(IEntityNormalizer<>));

            // Typed-only registrations (IEntityPrimer<T> / IEntityNormalizer<T> without the untyped
            // interface) are NOT returned by GetServices<IEntityPrimer>(); read their target straight from
            // the service type so a missing interceptor is still flagged for them. Duplicates are harmless
            // (Targets() only checks .Any()).
            primerTargets.AddRange(TypedServiceTargets(context.Services, typeof(IEntityPrimer<>)));
            normalizerTargets.AddRange(TypedServiceTargets(context.Services, typeof(IEntityNormalizer<>)));

            return (primerTargets, normalizerTargets, null);
        }
        catch (Exception ex)
        {
            return ([], [], ex.Message);
        }
    }

    // The target entity type read from every closed IEntityPrimer<T> / IEntityNormalizer<T> service-type
    // registration — no instance resolution required.
    private static IEnumerable<Type?> TypedServiceTargets(IServiceCollection services, Type genericMarker)
        => services
            .Where(d => !d.IsKeyedService
                        && d.ServiceType.IsGenericType && !d.ServiceType.IsGenericTypeDefinition
                        && d.ServiceType.GetGenericTypeDefinition() == genericMarker)
            .Select(d => (Type?)d.ServiceType.GetGenericArguments()[0]);

    private static List<Type?> TargetsOf<T>(IEnumerable<T> instances, Type genericMarker) where T : notnull
        => instances
            .Select(instance => instance.GetType().GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == genericMarker)
                .Select(i => (Type?)i.GetGenericArguments()[0])
                .FirstOrDefault())
            .ToList();

    // A context needs the interceptor when a registered primer/normalizer targets any of its entities
    // (or targets everything via a hook that implements only the non-generic marker).
    private static bool Targets(List<Type?> targets, Type[] contextEntities)
        => targets.Any(t => t is null || contextEntities.Any(e => t.IsAssignableFrom(e)));
}
