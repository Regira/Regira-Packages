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

        configure?.Invoke(attachmentBuilder);

        attachmentBuilder.RelatedAttachments(x => x.Attachments, entity => entity.Attachments?.SetSortOrder(), attachmentBuilder.HasStrictRelation);

        if (!attachmentBuilder.HasService<IEntityService<TEntityAttachment>>())
        {
            attachmentBuilder.AddDefaultAttachmentServices();
        }

        if (attachmentBuilder is { HasEntityAttachmentMapping: false, Options.EntityMapConfiguratorFactory: not null })
        {
            attachmentBuilder.WithDefaultMapping();
        }

        return attachmentBuilder;
    }
    public static EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment> HasAttachments<TContext, TEntity, TEntityAttachment>
    (
        this EntityServiceBuilder<TContext, TEntity, int> builder,
        Action<EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment>>? configure = null
    )
        where TContext : DbContext
        where TEntity : class, IEntity<int>, IHasAttachments<TEntityAttachment>
        where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>, new()
    {
        var attachmentBuilder = new EntityAttachmentServiceBuilder<TContext, TEntity, TEntityAttachment>(builder.Options);

        configure?.Invoke(attachmentBuilder);

        attachmentBuilder.RelatedAttachments(x => x.Attachments, entity => entity.Attachments?.SetSortOrder(), attachmentBuilder.HasStrictRelation);

        if (!attachmentBuilder.HasService<IEntityService<TEntityAttachment>>())
        {
            attachmentBuilder.AddDefaultAttachmentServices();
        }

        if (attachmentBuilder is { HasEntityAttachmentMapping: false, Options.EntityMapConfiguratorFactory: not null })
        {
            attachmentBuilder.WithDefaultMapping();
        }

        return attachmentBuilder;
    }

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

        configure?.Invoke(attachmentBuilder);

        attachmentBuilder.RelatedAttachments(x => x.Attachments, entity => entity.Attachments?.SetSortOrder(), attachmentBuilder.HasStrictRelation);

        if (!attachmentBuilder.HasService<IEntityService<TEntityAttachment, TEntityAttachmentKey>>())
        {
            attachmentBuilder.AddDefaultAttachmentServices();
        }

        if (attachmentBuilder is { HasEntityAttachmentMapping: false, Options.EntityMapConfiguratorFactory: not null })
        {
            attachmentBuilder.WithDefaultMapping();
        }

        return attachmentBuilder;
    }
}