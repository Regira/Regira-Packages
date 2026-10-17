using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.ServiceBuilders;
using Regira.Entities.Extensions;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.DependencyInjection.Attachments;

public static class EntityServiceBuilderExtensions
{
    // Attachments
    public static EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment> HasAttachments<TContext, TEntity, TEntityAttachment>
        (
            this EntityServiceBuilder<TContext, TEntity, int> builder,
            Expression<Func<TEntity, ICollection<TEntityAttachment>?>> navigationExpression,
            Action<EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment>>? configure = null
        )
        where TContext : DbContext
        where TEntity : class, IEntity<int>, IHasAttachments<TEntityAttachment>
        where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>, new()
    {
        var attachmentBuilder = new EntityAttachmentServiceBuilder<TContext, TEntity, TEntityAttachment>(builder.Options);
        return Attach(attachmentBuilder, navigationExpression, configure,
            () => attachmentBuilder.HasService<IEntityService<TEntityAttachment>>(), () => attachmentBuilder.AddDefaultAttachmentServices());
    }
    public static EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment> HasAttachments<TContext, TEntity, TEntityAttachment>
    (
        this EntityServiceBuilder<TContext, TEntity, int> builder,
        Action<EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment>>? configure = null
    )
        where TContext : DbContext
        where TEntity : class, IEntity<int>, IHasAttachments<TEntityAttachment>
        where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>, new()
        => builder.HasAttachments<TContext, TEntity, TEntityAttachment>(x => x.Attachments, configure);

    public static EntityAttachmentServiceBuilder<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>
        HasAttachments<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>
        (
            this EntityServiceBuilder<TContext, TEntity, TKey> builder,
            Action<EntityAttachmentServiceBuilder<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>>? configure = null
        )
        where TContext : DbContext
        where TEntity : class, IEntity<TKey>, IHasAttachments<TEntityAttachment, TEntityAttachmentKey, TKey, TAttachmentKey, TAttachment>
        where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TKey, TAttachmentKey, TAttachment>
        where TSearchObject : class, IEntityAttachmentSearchObject<TEntityAttachmentKey, TKey>, new()
        where TAttachment : class, IAttachment<TAttachmentKey>, new()
    {
        var attachmentBuilder = new EntityAttachmentServiceBuilder<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>(builder.Options);
        return Attach(attachmentBuilder, x => x.Attachments, configure,
            () => attachmentBuilder.HasService<IEntityService<TEntityAttachment, TEntityAttachmentKey>>(), () => attachmentBuilder.AddDefaultAttachmentServices());
    }

    /// <summary>
    /// The one body of every <c>HasAttachments</c>: the caller's configuration, the owner's sync of
    /// <paramref name="navigationExpression"/> (sort order from position), the link services unless configured, and
    /// the default mapping unless configured. <paramref name="hasServices"/> and <paramref name="addDefaultServices"/>
    /// are the shape's own: the int shape checks and registers the link entity's int-keyed services as well.
    /// </summary>
    private static EntityAttachmentServiceBuilder<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>
        Attach<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>(
            EntityAttachmentServiceBuilder<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment> attachmentBuilder,
            Expression<Func<TEntity, ICollection<TEntityAttachment>?>> navigationExpression,
            Action<EntityAttachmentServiceBuilder<TContext, TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>>? configure,
            Func<bool> hasServices, Action addDefaultServices)
        where TContext : DbContext
        where TEntity : class, IEntity<TKey>, IHasAttachments<TEntityAttachment, TEntityAttachmentKey, TKey, TAttachmentKey, TAttachment>
        where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TKey, TAttachmentKey, TAttachment>
        where TSearchObject : class, IEntityAttachmentSearchObject<TEntityAttachmentKey, TKey>, new()
        where TAttachment : class, IAttachment<TAttachmentKey>, new()
    {
        configure?.Invoke(attachmentBuilder);

        var navigation = navigationExpression.Compile();
        attachmentBuilder.RelatedAttachments(navigationExpression, entity => navigation(entity)?.SetSortOrder(), attachmentBuilder.HasStrictRelation);

        if (!hasServices())
        {
            addDefaultServices();
        }

        if (attachmentBuilder is { HasEntityAttachmentMapping: false, Options.EntityMapConfiguratorFactory: not null })
        {
            attachmentBuilder.WithDefaultMapping();
        }

        return attachmentBuilder;
    }
}
