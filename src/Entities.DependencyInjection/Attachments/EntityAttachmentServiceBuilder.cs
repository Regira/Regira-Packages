using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.DependencyInjection.Attachments.Abstractions;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Preppers;
using Regira.Entities.DependencyInjection.ServiceBuilders;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.Mapping.Abstractions;
using Regira.Entities.Mapping.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Services.Abstractions;
using System.Linq.Expressions;

namespace Regira.Entities.DependencyInjection.Attachments;

public class EntityAttachmentServiceBuilder<TContext, TEntity, TEntityAttachment>(EntityServiceCollectionOptions options)
    : EntityAttachmentServiceBuilder<TContext, TEntity, int, TEntityAttachment, int, EntityAttachmentSearchObject, int, Attachment>(options),
        IEntityAttachmentServiceBuilder<TEntity, TEntityAttachment>
    where TContext : DbContext
    where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>, new()
    where TEntity : class, IEntity<int>, IHasAttachments<TEntityAttachment>
{
    public new EntityAttachmentServiceBuilder<TContext, TEntity, TEntityAttachment> AddDefaultAttachmentServices()
    {
        For<TEntityAttachment>();

        base.AddDefaultAttachmentServices();

        return this;
    }
}

public class EntityAttachmentServiceBuilder<TContext, TObject, TObjectKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>(EntityServiceCollectionOptions options)
    : EntitySearchObjectServiceBuilder<TContext, TEntityAttachment, TEntityAttachmentKey, TSearchObject>(options),
        IEntityAttachmentServiceBuilder<TObject, TObjectKey, TEntityAttachment, TEntityAttachmentKey, TAttachmentKey, TAttachment>
    where TContext : DbContext
    where TObject : class, IEntity<TObjectKey>, IHasAttachments<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
    where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
    where TSearchObject : class, IEntityAttachmentSearchObject<TEntityAttachmentKey, TObjectKey>, new()
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    /// <summary>
    /// EntityAttachment and Attachment are strictly related (one on one), which implies removing the Attachment when removing the EntityAttachment
    /// Defaults to true
    /// </summary>
    public bool HasStrictRelation { get; set; } = true;
    public bool HasEntityAttachmentMapping { get; set; }

    /// <summary>
    /// Adds AutoMapper maps
    /// <list type="bullet">
    ///     <item><typeparamref name="TEntityAttachment"/> -&gt; <see cref="EntityAttachmentDto"/></item>
    ///     <item><see cref="EntityAttachmentInputDto"/> -&gt; <typeparamref name="TEntityAttachment"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public MappedEntityServiceBuilder<TContext, TEntityAttachment, TEntityAttachmentKey> WithDefaultMapping()
        => UseMapping<EntityAttachmentDto, EntityAttachmentInputDto>();
    /// <summary>
    /// Adds mapping configurations for the specified entity attachment DTO and input DTO types.
    /// </summary>
    /// <remarks>This method configures mappings between the entity attachment type and the specified DTO
    /// types. It also sets the <c>HasEntityAttachmentMapping</c> flag to <see langword="true"/>.</remarks>
    /// <typeparam name="TEntityAttachmentDto">The type of the entity attachment DTO to be mapped.</typeparam>
    /// <typeparam name="TEntityAttachmentInputDto">The type of the entity attachment input DTO to be mapped.</typeparam>
    /// <returns>The current <see cref="EntityAttachmentServiceBuilder{TContext, TObject, TObjectKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment}"/> instance, allowing for method chaining.</returns>
    public MappedEntityServiceBuilder<TContext, TEntityAttachment, TEntityAttachmentKey> UseMapping<TEntityAttachmentDto, TEntityAttachmentInputDto>()
        where TEntityAttachmentDto : EntityAttachmentDto
    {
        base.UseMapping<TEntityAttachmentDto, TEntityAttachmentInputDto>();

        // AfterMapper to resolve Uri.
        // The concrete (ASP.NET Core) resolver is plugged in from the web layer via UseAttachmentUris().
        // Without it, attachment DTOs get a null Uri instead of forcing an ASP.NET Core dependency.
        if (Options.AttachmentUriRegistrar != null)
        {
            Options.AttachmentUriRegistrar.Register<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(Services);
        }
        else
        {
            AddTransient<IAttachmentUriResolver<TEntityAttachment>>(_ => new NullAttachmentUriResolver<TEntityAttachment>());
        }
        AddTransient<IEntityAfterMapper>(p => new EntityAfterMapper<TEntityAttachment, TEntityAttachmentDto>((item, dto) =>
        {
            var uriResolver = p.GetRequiredService<IAttachmentUriResolver<TEntityAttachment>>();
            dto.Uri = uriResolver.Resolve(item);
        }));
        AddTransient<IEntityAfterMapper, EntityAttachmentUriAfterMapper<TObject, TEntityAttachment, TEntityAttachmentDto, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>>();
        
        HasEntityAttachmentMapping = true;

        return new MappedEntityServiceBuilder<TContext, TEntityAttachment, TEntityAttachmentKey>(Options);
    }


    /// <summary>
    /// Adds implementations for
    /// <list type="bullet">
    ///     <item><see cref="IEntityService{TEntityAttachment}"/></item>
    ///     <item><see cref="IEntityService{TEntityAttachment,TKey}"/></item>
    /// </list>
    /// </summary>
    /// <returns></returns>
    public EntityAttachmentServiceBuilder<TContext, TObject, TObjectKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment> AddDefaultAttachmentServices()
    {
        For<TEntityAttachment, TEntityAttachmentKey, TSearchObject>(e =>
        {
            e.Includes((query, _) => query.Include(x => x.Attachment));
            // SortOrder is the natural key here — the owner's save stamps it from array position, so an
            // owner's attachments come back in the order the owner arranged them. Without a default the list
            // is unordered while paging still applies a Take, which EF reports as a row-limiting-operator
            // warning on every request that consumers cannot silence: the per-owner services are registered by
            // HasAttachments, so there is no .For<>() of theirs to hang a SortBy on.
            // Id breaks the tie, and it is not optional: only the owner-save path writes SortOrder, so rows
            // uploaded through POST /{owner}/{id}/files all carry 0 until the owner is saved. Ordering by an
            // all-equal key leaves the tiebreak to the provider, which is the same unstable paging the warning
            // was pointing at — Skip/Take could repeat a row on one page and drop it from the next.
            e.SortBy(query => query.OrderBy(x => x.SortOrder).ThenBy(x => x.Id));
            e.AddFilter<EntityAttachmentFilteredQueryBuilder<TObjectKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>>();
            e.AddProcessor<EntityAttachmentProcessor<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>>();
            e.AddPrepper<EntityAttachmentPrepper<TContext, TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>>();
            e.UseWriteService<EntityAttachmentWriteService<TContext, TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>>();
        });

        return this;
    }

    // Related
    public EntityAttachmentServiceBuilder<TContext, TObject, TObjectKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment> RelatedAttachments(
        Expression<Func<TObject, ICollection<TEntityAttachment>?>> navigationExpression, Action<TObject>? prepareFunc = null, bool isStrictRelation = true)
    {
        Services.AddPrepper(p => new RelatedAttachmentsPrepper<TContext, TObject, TEntityAttachment, TObjectKey, TEntityAttachmentKey, TAttachmentKey, TAttachment>(
            p.GetRequiredService<TContext>(),
            navigationExpression,
            new RelatedAttachmentsPrepper<TContext, TObject, TEntityAttachment, TObjectKey, TEntityAttachmentKey, TAttachmentKey, TAttachment>.Options { IsStrictRelation = isStrictRelation })
        );

        if (prepareFunc != null)
        {
            Services.AddPrepper(prepareFunc);
        }

        return this;
    }
}
