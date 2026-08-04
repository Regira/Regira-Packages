using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.ServiceBuilders;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.Models.Abstractions;
using Regira.IO.Storage.Abstractions;

namespace Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;

public interface IEntityServiceCollection<TContext>
    where TContext : DbContext
{
    public EntityServiceCollection<TContext> For<TEntity>(Action<EntityIntServiceBuilder<TContext, TEntity>>? configure = null)
        where TEntity : class, IEntity<int>;

    /// <summary>
    /// <inheritdoc cref="EntityServiceBuilder{TContext,TEntity,TKey}.AddDefaultService"/>
    /// </summary>
    /// <typeparam name="TEntity"></typeparam>
    /// <typeparam name="TKey"></typeparam>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EntityServiceCollection<TContext> For<TEntity, TKey>(Action<EntityServiceBuilder<TContext, TEntity, TKey>>? configure = null)
        where TEntity : class, IEntity<TKey>;

    public EntityServiceCollection<TContext> For<TEntity, TKey, TSearchObject>(Action<EntitySearchObjectServiceBuilder<TContext, TEntity, TKey, TSearchObject>>? configure = null)
        where TEntity : class, IEntity<TKey>
        where TSearchObject : class, ISearchObject<TKey>, new();

    public EntityServiceCollection<TContext> For<TEntity, TSearchObject, TSortBy, TIncludes>
        (Action<ComplexEntityIntServiceBuilder<TContext, TEntity, TSearchObject, TSortBy, TIncludes>>? configure = null)
        where TEntity : class, IEntity<int>
        where TSearchObject : class, ISearchObject<int>, new()
        where TSortBy : struct, Enum
        where TIncludes : struct, Enum;

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
        where TIncludes : struct, Enum;

    public EntityServiceCollection<TContext> WithAttachments(Func<IServiceProvider, IFileService> factory, Action<EntitySearchObjectServiceBuilder<TContext, Attachment, int, AttachmentSearchObject<int>>>? configure = null);

    /// <summary>
    /// Adds <see cref="IAttachmentFileService{TAttachment, TKey}"/> to <see cref="IServiceCollection"/> with an implementation of <see cref="IFileService"/>.<br />
    /// </summary>
    /// <param name="factory"></param>
    /// <param name="configure"></param>
    /// <returns></returns>
    public EntityServiceCollection<TContext> WithAttachments<TAttachment, TKey, TAttachmentSearchObject>(
        Func<IServiceProvider, IFileService> factory,
        Action<EntitySearchObjectServiceBuilder<TContext, TAttachment, TKey, TAttachmentSearchObject>>? configure = null
    )
        where TAttachment : class, IAttachment<TKey>, new()
        where TAttachmentSearchObject : AttachmentSearchObject<TKey>, new();

    /// <summary>
    /// Adds <see cref="ITypedAttachmentService"/> to <see cref="IServiceCollection"/>
    /// using a collection of <see cref="AttachmentQuerySetDescriptor{T}"/>
    /// </summary>
    /// <param name="queryFactory"></param>
    /// <returns></returns>
    public EntityServiceCollection<TContext> ConfigureTypedAttachmentService(Func<TContext, IList<IAttachmentQuerySetDescriptor>> queryFactory);

    public EntityServiceCollection<TContext> ConfigureTypedAttachmentService<TService>()
        where TService : class, ITypedAttachmentService;

    public EntityServiceCollection<TContext> AddTransient<TService, TImplementation>()
        where TService : class
        where TImplementation : class, TService;

    public EntityServiceCollection<TContext> AddTransient<TService>(Func<IServiceProvider, TService> factory)
        where TService : class;

    IServiceCollection Services { get; }
}