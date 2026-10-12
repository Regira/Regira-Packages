using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Regira.Entities.DependencyInjection.Licensing;
using Regira.Entities.DependencyInjection.Primers;
using Regira.Entities.DependencyInjection.Reactors;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.ServiceBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using Regira.IO.Storage.Abstractions;
namespace Regira.Entities.DependencyInjection.ServiceCollections;

public class EntityServiceCollection<TContext>(EntityServiceCollectionOptions options) : ServiceCollectionWrapper(options.Services), IEntityServiceCollection<TContext> where TContext : DbContext
{
    protected internal EntityServiceCollectionOptions Options => options;

    private readonly EntityLicenseValidator _licenseValidator = EntityLicenseValidator.For(options.Services);
    private readonly EntityRegistrationLog _registrationLog = EntityRegistrationLog.ForContext(options.Services, typeof(TContext));


    // Default service
    public EntityServiceCollection<TContext> For<TEntity>(Action<EntityIntServiceBuilder<TContext, TEntity>>? configure = null)
        where TEntity : class, IEntity<int>
    {
        _licenseValidator.TrackSimple(options.Services, typeof(TEntity));
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(int), typeof(SearchObject<int>), isComplex: false);
        var builder = new EntityIntServiceBuilder<TContext, TEntity>(Options);
        var configuredFrom = Services.Count;
        configure?.Invoke(builder);

        // Query Builder
        if (!builder.HasService<IQueryBuilder<TEntity, int, SearchObject<int>, EntitySortBy, EntityIncludes>>())
        {
            builder.AddDefaultQueryBuilder();
        }
        // Read Service
        if (!builder.HasService<IEntityReadService<TEntity, int, SearchObject<int>>>())
        {
            builder.UseReadService<EntityReadService<TContext, TEntity, int, SearchObject<int>>>();
        }
        // Write Service
        if (!builder.HasService<IEntityWriteService<TEntity, int>>())
        {
            builder.UseWriteService<EntityWriteService<TContext, TEntity>>();
        }
        // Entity Repository + Entity Service
        AddDefaultRepository(typeof(EntityRepository<TEntity>),
            asRepository: !builder.HasService<IEntityRepository<TEntity>>(),
            asEntityService: !builder.HasService<IEntityService<TEntity>>(),
            configuredFrom);

        return this;
    }
    /// <summary>
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.AddDefaultService"/>
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EntityServiceCollection<TContext> For<TEntity, TKey>(Action<EntityServiceBuilder<TContext, TEntity, TKey>>? configure = null)
        where TEntity : class, IEntity<TKey>
    {
        _licenseValidator.TrackSimple(options.Services, typeof(TEntity));
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(TKey), typeof(SearchObject<TKey>), isComplex: false);
        var builder = new EntityServiceBuilder<TContext, TEntity, TKey>(options);
        var configuredFrom = Services.Count;
        configure?.Invoke(builder);

        EnsureGlobalKeyedFilters<TKey>();

        // Query Builder
        if (!builder.HasService<IQueryBuilder<TEntity, TKey, SearchObject<TKey>, EntitySortBy, EntityIncludes>>())
        {
            builder.AddDefaultQueryBuilder();
        }
        // Read Service
        if (!builder.HasService<IEntityReadService<TEntity, TKey, SearchObject<TKey>>>())
        {
            builder.UseReadService<EntityReadService<TContext, TEntity, TKey, SearchObject<TKey>>>();
        }
        // Write Service
        if (!builder.HasService<IEntityWriteService<TEntity, TKey>>())
        {
            builder.UseWriteService<EntityWriteService<TContext, TEntity, TKey>>();
        }
        // Entity Repository + Entity Service
        AddDefaultRepository(typeof(EntityRepository<TEntity, TKey>),
            asRepository: !builder.HasService<IEntityRepository<TEntity, TKey>>(),
            asEntityService: !builder.HasService<IEntityService<TEntity, TKey>>(),
            configuredFrom);

        return this;
    }
    public EntityServiceCollection<TContext> For<TEntity, TKey, TSearchObject>(Action<EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
    {
        _licenseValidator.TrackSimple(options.Services, typeof(TEntity));
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(TKey), typeof(TSearchObject), isComplex: false);
        var builder = new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>(Options);
        var configuredFrom = Services.Count;
        configure?.Invoke(builder);

        EnsureGlobalKeyedFilters<TKey>();

        // Query Builder
        if (!builder.HasService<IQueryBuilder<TEntity, TKey, TSearchObject, EntitySortBy, EntityIncludes>>())
        {
            builder.AddDefaultQueryBuilder();
        }
        // Read Service
        if (!builder.HasService<IEntityReadService<TEntity, TKey, TSearchObject>>())
        {
            builder.UseReadService<EntityReadService<TContext, TEntity, TKey, TSearchObject>>();
        }
        // Write Service
        if (!builder.HasService<IEntityWriteService<TEntity, TKey>>())
        {
            builder.UseWriteService<EntityWriteService<TContext, TEntity, TKey>>();
        }
        // Entity Repository + Entity Service
        AddDefaultRepository(typeof(EntityRepository<TEntity, TKey, TSearchObject>),
            asRepository: !builder.HasService<IEntityRepository<TEntity, TKey, TSearchObject>>(),
            asEntityService: !builder.HasService<IEntityService<TEntity, TKey, TSearchObject>>(),
            configuredFrom);

        return this;
    }
    public EntityServiceCollection<TContext> For<TEntity, TSearchObject, TSortBy, TIncludes>
        (Action<ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>>? configure = null)
        where TEntity : class, IEntity<int>
        where TSearchObject : class, ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        _licenseValidator.TrackComplex(options.Services, typeof(TEntity));
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(int), typeof(TSearchObject), typeof(TSortBy), typeof(TIncludes));
        var simpleBuilder = new EntitySearchObjectServiceBuilder<TContext, TEntity, int, TSearchObject>(Options);
        var builder = new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>(simpleBuilder);
        var configuredFrom = Services.Count;
        configure?.Invoke(builder);

        // Query Builder
        if (!builder.HasService<IQueryBuilder<TEntity, int, TSearchObject, TSortBy, TIncludes>>())
        {
            builder.AddDefaultQueryBuilder();
        }

        // Read Service
        if (!builder.HasService<IEntityReadService<TEntity, int, TSearchObject, TSortBy, TIncludes>>())
        {
            builder.UseReadService<EntityReadService<TContext, TEntity, TSearchObject, TSortBy, TIncludes>>();
        }
        // Write Service
        if (!builder.HasService<IEntityWriteService<TEntity, int>>())
        {
            builder.UseWriteService<EntityWriteService<TContext, TEntity>>();
        }

        // Entity Repository + Entity Service
        AddDefaultRepository(typeof(EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>),
            asRepository: !builder.HasService<IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>(),
            asEntityService: !builder.HasEntityService(),
            configuredFrom);

        return this;
    }
    /// <summary>
    /// 
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    /// <typeparam name="TSearchObject"></typeparam>
    /// <typeparam name="TSortBy"></typeparam>
    /// <typeparam name="TIncludes"></typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EntityServiceCollection<TContext> For<TEntity, TKey, TSearchObject, TSortBy, TIncludes>
        (Action<ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum
    {
        _licenseValidator.TrackComplex(options.Services, typeof(TEntity));
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(TKey), typeof(TSearchObject), typeof(TSortBy), typeof(TIncludes));
        var simpleBuilder = new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>(Options);
        var builder = new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>(simpleBuilder);
        var configuredFrom = Services.Count;
        configure?.Invoke(builder);

        EnsureGlobalKeyedFilters<TKey>();

        // Query Builder
        if (!builder.HasService<IQueryBuilder<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>())
        {
            builder.AddDefaultQueryBuilder();
        }

        // Read Service
        if (!builder.HasService<IEntityReadService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>())
        {
            builder.UseReadService<EntityReadService<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        }
        // Write Service
        if (!builder.HasService<IEntityWriteService<TEntity, TKey>>())
        {
            builder.UseWriteService<EntityWriteService<TContext, TEntity, TKey>>();
        }

        // Entity Repository + Entity Service
        AddDefaultRepository(typeof(EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>),
            asRepository: !builder.HasService<IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>(),
            asEntityService: !builder.HasEntityService(),
            configuredFrom);

        return this;
    }


    // Service with attachments
    public EntityServiceCollection<TContext> WithAttachments(Func<IServiceProvider, IFileService> factory, Action<EntitySearchObjectServiceBuilder<TContext, Attachment, int, AttachmentSearchObject<int>>>? configure = null)
    {
        return WithAttachments<Attachment, int, AttachmentSearchObject<int>>(factory, configure);
    }

    public EntityServiceCollection<TContext> WithAttachments<TAttachment, TAttachmentKey, TAttachmentSearchObject>(
        Func<IServiceProvider, IFileService> fileServiceFactory,
        Action<EntitySearchObjectServiceBuilder<TContext, TAttachment, TAttachmentKey, TAttachmentSearchObject>>? configure = null
    )
        where TAttachment : class, IAttachment<TAttachmentKey>, new()
        where TAttachmentSearchObject : AttachmentSearchObject<TAttachmentKey>, new()
    {
        var builder = new EntitySearchObjectServiceBuilder<TContext, TAttachment, TAttachmentKey, TAttachmentSearchObject>(Options);

        builder.AddPrimer<EntityAttachmentPrimer>();

        builder.For<TAttachment, TAttachmentKey, TAttachmentSearchObject>(e =>
        {
            e.AddFilter<AttachmentFilteredQueryBuilder<TAttachment, TAttachmentKey, TAttachmentSearchObject>>();
            e.AddProcessor<AttachmentProcessor<TAttachment, TAttachmentKey>>();
            // the file reactor reads the attachment's row in the reaction's own context before it removes a file, so it
            // is registered through a factory, which names no implementation type
            if (typeof(TAttachment) == typeof(Attachment))
            {
                // the default attachment registers the non-generic types, the primer by type for code finding or replacing it
                Services.AddPrimer<Attachment, AttachmentPrimer>();
                Services.AddReactor<Attachment>(p => new AttachmentFileReactor(
                    p.GetRequiredService<IAttachmentFileService<Attachment, int>>(), p.GetRequiredService<TContext>()));
            }
            else
            {
                e.AddPrimer<AttachmentPrimer<TAttachment, TAttachmentKey>>();
                Services.AddReactor<TAttachment>(p => new AttachmentFileReactor<TAttachment, TAttachmentKey>(
                    p.GetRequiredService<IAttachmentFileService<TAttachment, TAttachmentKey>>(), p.GetRequiredService<TContext>()));
            }
            e.AddTransient<IFileIdentifierGenerator, DefaultFileIdentifierGenerator<TAttachmentKey, TAttachment>>();
            e.AddTransient<IAttachmentFileService<TAttachment, TAttachmentKey>>(p => new AttachmentFileService<TAttachment, TAttachmentKey>(fileServiceFactory(p)));
            configure?.Invoke(e);
        });

        // mappings
        if (Options.EntityMapConfiguratorFactory != null)
        {
            var mapConfig = Options.EntityMapConfiguratorFactory.Invoke(Services);
            mapConfig.Configure<Attachment, AttachmentDto>();
            mapConfig.Configure<Attachment, AttachmentDto<int>>();
            mapConfig.Configure(typeof(Attachment<>), typeof(AttachmentDto<>));
            mapConfig.Configure<AttachmentInputDto, Attachment>();
            mapConfig.Configure<AttachmentInputDto<int>, Attachment>();
            mapConfig.Configure(typeof(AttachmentInputDto<>), typeof(Attachment<>));
            mapConfig.Configure<EntityAttachment, EntityAttachmentDto>();
            mapConfig.Configure(typeof(EntityAttachment<,,,>), typeof(EntityAttachmentDto<,,>));
            mapConfig.Configure<EntityAttachmentInputDto, EntityAttachment>();
            mapConfig.Configure(typeof(EntityAttachmentInputDto<,,>), typeof(EntityAttachment<,,,>));
        }

        return this;
    }
    /// <summary>
    /// Adds <see cref="ITypedAttachmentService"/> to <see cref="IServiceCollection"/>
    /// using a collection of <see cref="AttachmentQuerySetDescriptor{T}"/>
    /// </summary>
    /// <param name="queryFactory"></param>
    /// <returns></returns>
    public EntityServiceCollection<TContext> ConfigureTypedAttachmentService(Func<TContext, IList<IAttachmentQuerySetDescriptor>> queryFactory)
    {
        Services.AddTransient<ITypedAttachmentService>(p
            => new TypedAttachmentService<TContext>(p.GetRequiredService<TContext>(), queryFactory));
        return this;
    }
    public EntityServiceCollection<TContext> ConfigureTypedAttachmentService<TService>()
        where TService : class, ITypedAttachmentService
    {
        Services.AddTransient<ITypedAttachmentService, TService>();
        return this;
    }


    // helpers

    /// <summary>
    /// Registers the repository of a registration that named none: <paramref name="defaultRepositoryType"/>, or the
    /// app's own type of the same shape set with <see cref="EntityServiceCollectionOptions.UseRepository"/> — under the
    /// repository and/or entity-service interfaces the default implements.
    /// </summary>
    /// <exception cref="InvalidOperationException">The configuration, from <paramref name="configuredFrom"/> on, registered
    /// an implementation under some of those interfaces but not under the one this shape resolves through: the default
    /// would replace it under all of them, silently.</exception>
    private void AddDefaultRepository(Type defaultRepositoryType, bool asRepository, bool asEntityService, int configuredFrom)
    {
        if (!asRepository && !asEntityService)
        {
            return;
        }

        var serviceTypes = EntityRepositoryRegistry.ServiceTypesOf(defaultRepositoryType, asRepository, asEntityService).ToArray();
        var configured = options.Services.Skip(configuredFrom).FirstOrDefault(d => serviceTypes.Contains(d.ServiceType));
        if (configured != null)
        {
            var entity = defaultRepositoryType.GetGenericArguments()[0].Name;
            var implementation = configured.ImplementationType ?? configured.ImplementationInstance?.GetType();
            var missing = serviceTypes.Where(t => options.Services.All(d => d.ServiceType != t)).Select(EntityRepositoryRegistry.FriendlyName);
            throw new InvalidOperationException(
                $"{(implementation != null ? EntityRepositoryRegistry.FriendlyName(implementation) : "A factory registration")} serves {entity} as " +
                $"{EntityRepositoryRegistry.FriendlyName(configured.ServiceType)} but not as {string.Join(", ", missing)}, which this For<>() resolves through, " +
                $"so the default {EntityRepositoryRegistry.FriendlyName(defaultRepositoryType)} would replace it under every interface. " +
                $"Derive it from the {EntityRepositoryRegistry.FriendlyName(defaultRepositoryType.GetGenericTypeDefinition())} of this shape, " +
                "or register the entity with the For<>() shape the class is written for.");
        }

        var repositoryType = EntityRepositoryRegistry.For(options.Services).Resolve(defaultRepositoryType);
        foreach (var serviceType in serviceTypes)
        {
            options.Services.AddTransient(serviceType, repositoryType);
        }
    }

    /// <summary>
    /// The default global filters are keyed to <see cref="int"/> and can only bind an int-keyed search
    /// object. For a non-int key each such filter drops its keyed criteria (an int filter receiving a
    /// <c>SearchObject&lt;Guid&gt;</c> null-coerces the keyed fields): Id/Ids/Exclude, Archived,
    /// Min/MaxCreated, Min/MaxLastModified, and the <c>?q=</c> normalized-content search. So for a non-int
    /// key we backfill the matching keyed variant of every default filter that is registered — otherwise a
    /// Guid entity's <c>?archived=only</c>/<c>?minCreated=</c>/<c>?q=</c> silently return the wrong rows
    /// (and Details(id) would return the whole table and throw on SingleOrDefault). Only the filters whose
    /// int default is present are backfilled, so setups that removed global filters stay untouched. The
    /// selector deduplicates the int and keyed variants into one family and runs the key-matching one.
    /// </summary>
    private void EnsureGlobalKeyedFilters<TKey>()
    {
        if (typeof(TKey) == typeof(int))
        {
            return;
        }

        var registered = options.Services
            .Where(s => s.ServiceType == typeof(IGlobalFilteredQueryBuilder))
            .Select(s => s.ImplementationType)
            .Where(t => t != null)
            .ToHashSet();

        EnsureKeyedVariant<FilterIdsQueryBuilder<int>, FilterIdsQueryBuilder<TKey>>(registered);
        EnsureKeyedVariant<FilterArchivablesQueryBuilder, FilterArchivablesQueryBuilder<TKey>>(registered);
        EnsureKeyedVariant<FilterHasCreatedQueryBuilder, FilterHasCreatedQueryBuilder<TKey>>(registered);
        EnsureKeyedVariant<FilterHasLastModifiedQueryBuilder, FilterHasLastModifiedQueryBuilder<TKey>>(registered);
        EnsureKeyedVariant<FilterHasNormalizedContentQueryBuilder, FilterHasNormalizedContentQueryBuilder<TKey>>(registered);
    }

    private void EnsureKeyedVariant<TIntDefault, TKeyed>(HashSet<Type?> registered)
        where TIntDefault : class, IGlobalFilteredQueryBuilder
        where TKeyed : class, IGlobalFilteredQueryBuilder
    {
        if (registered.Contains(typeof(TIntDefault)) && !registered.Contains(typeof(TKeyed)))
        {
            // back-filled only because the defaults registered the int variant
            options.AddGlobalFilterQueryBuilder<TKeyed>();
        }
    }

    public EntityServiceCollection<TContext> AddTransient<TService>()
        where TService : class
        => AddTransient<TService, TService>();
    public EntityServiceCollection<TContext> AddTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService
    {
        Services.AddTransient<TService, TImplementation>();

        return this;
    }
    public EntityServiceCollection<TContext> AddTransient<TService>(Func<IServiceProvider, TService> factory)
        where TService : class
    {
        Services.AddTransient(factory);

        return this;
    }
}