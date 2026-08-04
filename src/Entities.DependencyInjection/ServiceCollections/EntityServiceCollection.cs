using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Regira.Entities.DependencyInjection.Licensing;
using Regira.Entities.DependencyInjection.Primers;
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
        // Entity Repository
        if (!builder.HasService<IEntityRepository<TEntity>>())
        {
            builder.HasRepositoryInner<EntityRepository<TEntity>>();
        }
        // Entity Service
        if (!builder.HasService<IEntityService<TEntity>>())
        {
            builder.UseEntityService<EntityRepository<TEntity>>();
        }

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
        // Entity Repository
        if (!builder.HasService<IEntityRepository<TEntity, TKey>>())
        {
            builder.HasRepositoryInner<EntityRepository<TEntity, TKey>>();
        }
        // Entity Service
        if (!builder.HasService<IEntityService<TEntity, TKey>>())
        {
            builder.UseEntityService<EntityRepository<TEntity, TKey>>();
        }

        return this;
    }
    public EntityServiceCollection<TContext> For<TEntity, TKey, TSearchObject>(Action<EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new()
    {
        _licenseValidator.TrackSimple(options.Services, typeof(TEntity));
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(TKey), typeof(TSearchObject), isComplex: false);
        var builder = new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>(Options);
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
        // Entity Repository
        if (!builder.HasService<IEntityRepository<TEntity, TKey, TSearchObject>>())
        {
            builder.HasRepositoryInner<EntityRepository<TEntity, TKey, TSearchObject>>();
        }
        // Entity Service
        if (!builder.HasService<IEntityService<TEntity, TKey, TSearchObject>>())
        {
            builder.UseEntityService<EntityRepository<TEntity, TKey, TSearchObject>>();
        }

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
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(int), typeof(TSearchObject), isComplex: true);
        var simpleBuilder = new EntitySearchObjectServiceBuilder<TContext, TEntity, int, TSearchObject>(Options);
        var builder = new ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>(simpleBuilder);
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

        // Entity Repository
        if (!builder.HasService<IEntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>())
        {
            builder.HasRepositoryInner<EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();
        }

        // Entity Service
        if (!builder.HasEntityService())
        {
            builder.AddTransient<IEntityService<TEntity>, EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();
            builder.AddTransient<IEntityService<TEntity, TSearchObject, TSortBy, TIncludes>, EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();

            builder.AddTransient<IEntityService<TEntity, int>, EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();
            builder.AddTransient<IEntityService<TEntity, int, TSearchObject>, EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();
            builder.AddTransient<IEntityService<TEntity, int, TSearchObject, TSortBy, TIncludes>, EntityRepository<TEntity, TSearchObject, TSortBy, TIncludes>>();
        }

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
        _registrationLog.TrackEntity(typeof(TContext), typeof(TEntity), typeof(TKey), typeof(TSearchObject), isComplex: true);
        var simpleBuilder = new EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>(Options);
        var builder = new ComplexEntityServiceBuilder<TContext, TEntity, TKey, TSearchObject, TSortBy, TIncludes>(simpleBuilder);
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

        // Entity Repository
        if (!builder.HasService<IEntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>())
        {
            builder.HasRepositoryInner<EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        }

        // Entity Service
        if (!builder.HasEntityService())
        {
            builder.AddTransient<IEntityService<TEntity, TKey>, EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
            builder.AddTransient<IEntityService<TEntity, TKey, TSearchObject>, EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
            builder.AddTransient<IEntityService<TEntity, TKey, TSearchObject, TSortBy, TIncludes>, EntityRepository<TEntity, TKey, TSearchObject, TSortBy, TIncludes>>();
        }

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
            e.AddPrimer<AttachmentPrimer>();
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
            // back-filled only because the defaults registered the int variant, so this is
            // defaults-contributed too — it must be recorded as such, or the scope check reports an inert
            // keyed twin as a hand-registered filter and warns about rows the app never meant to hide
            options.AddDefaultGlobalFilter<TKeyed>();
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