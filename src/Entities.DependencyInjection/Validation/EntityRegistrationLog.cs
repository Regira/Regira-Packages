using Microsoft.Extensions.DependencyInjection;

namespace Regira.Entities.DependencyInjection.Validation;

/// <summary>
/// Records what flows through <c>UseEntities&lt;TContext&gt;()</c> and <c>For&lt;&gt;()</c> during DI
/// configuration, so the startup validators can compare intent (registrations) against the container.
/// Registered as an instance singleton the first time it is requested for a service collection.
/// </summary>
public sealed class EntityRegistrationLog
{
    public sealed record EntityRegistration(Type ContextType, Type EntityType, Type KeyType, Type SearchObjectType, bool IsComplex);

    /// <summary>A child collection synced by a parent's <c>Related()</c> — i.e. an entity the parent owns.</summary>
    public sealed record RelatedRegistration(Type ContextType, Type ParentType, Type RelatedType);

    public IList<EntityRegistration> Entities { get; } = [];
    public IList<RelatedRegistration> Related { get; } = [];
    public IList<Type> ContextTypes { get; } = [];

    /// <summary>
    /// Global filters contributed by <c>UseDefaults()</c> / <c>AddDefaultGlobalQueryFilters()</c> rather than
    /// chosen by the app. The whole set is registered regardless of which capability interfaces the entities
    /// implement, so an inert one carries no signal — while the same type registered deliberately does.
    /// </summary>
    public ISet<Type> DefaultGlobalFilters { get; } = new HashSet<Type>();

    public static EntityRegistrationLog For(IServiceCollection services)
    {
        if (services.FirstOrDefault(d => d.ServiceType == typeof(EntityRegistrationLog))?.ImplementationInstance is EntityRegistrationLog existing)
        {
            return existing;
        }
        var log = new EntityRegistrationLog();
        services.AddSingleton(log);
        return log;
    }

    /// <summary>Gets (or creates) the log for <paramref name="services"/> and tracks <paramref name="contextType"/>.</summary>
    public static EntityRegistrationLog ForContext(IServiceCollection services, Type contextType)
    {
        var log = For(services);
        log.TrackContext(contextType);
        return log;
    }

    public void TrackContext(Type contextType)
    {
        if (!ContextTypes.Contains(contextType))
        {
            ContextTypes.Add(contextType);
        }
    }

    public void TrackEntity(Type contextType, Type entityType, Type keyType, Type searchObjectType, bool isComplex)
    {
        var registration = new EntityRegistration(contextType, entityType, keyType, searchObjectType, isComplex);
        if (!Entities.Contains(registration))
        {
            Entities.Add(registration);
        }
    }

    public void TrackRelated(Type contextType, Type parentType, Type relatedType)
    {
        var registration = new RelatedRegistration(contextType, parentType, relatedType);
        if (!Related.Contains(registration))
        {
            Related.Add(registration);
        }
    }

    /// <summary>Records a <c>Related()</c> call against the log for <paramref name="services"/>.</summary>
    public static void TrackRelated(IServiceCollection services, Type contextType, Type parentType, Type relatedType)
        => For(services).TrackRelated(contextType, parentType, relatedType);

    /// <summary>Records that <paramref name="filterType"/> was contributed by the defaults, not chosen by the app.</summary>
    public static void TrackDefaultGlobalFilter(IServiceCollection services, Type filterType)
        => For(services).DefaultGlobalFilters.Add(filterType);
}
