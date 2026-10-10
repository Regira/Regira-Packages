using Regira.DAL.Paging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Mediator.Abstractions;
using Regira.Entities.Mediator.Requests;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Models;

namespace Regira.Entities.Mediator.Handlers;

/// <summary>The default handler of <see cref="UploadAttachmentCommand{TEntity,TDto,TInputDto}"/>.</summary>
public class UploadAttachmentHandler<TEntity, TDto, TInputDto>(IServiceProvider services)
    : IEntityRequestHandler<UploadAttachmentCommand<TEntity, TDto, TInputDto>, SaveResult<TDto>>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<SaveResult<TDto>?> Handle(UploadAttachmentCommand<TEntity, TDto, TInputDto> request, CancellationToken token = default)
    {
        if (request.ValidateInput && request.Input is { } input)
        {
            Services.ThrowIfInvalidInput<TEntity>(input);
        }
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, int>>();
        var mapper = Services.GetMapper();

        var item = mapper.Map<TEntity>(request.Input!);
        // the route creates a link: an Id in the input cannot turn the upload into a write to another one
        item.Id = default;
        item.ObjectId = request.ObjectId;
        item.Attachment = request.File.ToAttachment();

        await service.Save(item, token);
        var affected = await service.SaveChanges(token);

        return new SaveResult<TDto> { Item = mapper.Map<TDto>(item), Affected = affected, IsNew = true };
    }
}

/// <summary>The default handler of <see cref="UpdateAttachmentCommand{TEntity,TDto,TInputDto}"/>.</summary>
public class UpdateAttachmentHandler<TEntity, TDto, TInputDto>(IServiceProvider services)
    : IEntityRequestHandler<UpdateAttachmentCommand<TEntity, TDto, TInputDto>, SaveResult<TDto>>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<SaveResult<TDto>?> Handle(UpdateAttachmentCommand<TEntity, TDto, TInputDto> request, CancellationToken token = default)
    {
        if (request.ValidateInput && request.Input is { } input)
        {
            Services.ThrowIfInvalidInput<TEntity>(input);
        }
        var mapper = Services.GetMapper();
        var item = mapper.Map<TEntity>(request.Input!);
        // the route is authoritative — the input can neither target another row nor reparent it
        item.Id = request.Id;
        item.ObjectId = request.ObjectId;

        var service = Services.GetRequiredEntityService<IEntityService<TEntity, int>>();
        var original = await service.FetchLink(request.Id, token);
        if (original == null)
        {
            return null;
        }
        if (original.ObjectId != request.ObjectId)
        {
            throw AttachmentLinks.NotALinkOfThisOwner<TEntity>();
        }

        await service.Save(item, token);
        var affected = await service.SaveChanges(token);

        var savedItem = await service.FetchLink(request.Id, token);
        return new SaveResult<TDto> { Item = mapper.Map<TDto>(savedItem!), Affected = affected, IsNew = false };
    }
}

/// <summary>The default handler of <see cref="ReplaceAttachmentFileCommand{TEntity,TDto}"/>.</summary>
public class ReplaceAttachmentFileHandler<TEntity, TDto>(IServiceProvider services)
    : IEntityRequestHandler<ReplaceAttachmentFileCommand<TEntity, TDto>, SaveResult<TDto>>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<SaveResult<TDto>?> Handle(ReplaceAttachmentFileCommand<TEntity, TDto> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, int>>();
        var mapper = Services.GetMapper();

        var item = await service.FetchLink(request.Id, token);
        if (item == null)
        {
            return null;
        }
        if (item.ObjectId != request.ObjectId)
        {
            throw AttachmentLinks.NotALinkOfThisOwner<TEntity>();
        }

        item.ObjectId = request.ObjectId;
        item.Attachment = request.File.ToAttachment();

        await service.Save(item, token);
        var affected = await service.SaveChanges(token);

        return new SaveResult<TDto> { Item = mapper.Map<TDto>(item), Affected = affected, IsNew = false };
    }
}

/// <summary>The default handler of <see cref="AttachmentFileQuery{TEntity}"/>.</summary>
public class AttachmentFileHandler<TEntity>(IServiceProvider services)
    : IEntityRequestHandler<AttachmentFileQuery<TEntity>, Attachment>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<Attachment?> Handle(AttachmentFileQuery<TEntity> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, int>>();
        return (await service.Details(request.Id, token))?.Attachment;
    }
}

/// <summary>The default handler of <see cref="AttachmentFileByNameQuery{TEntity}"/>.</summary>
public class AttachmentFileByNameHandler<TEntity>(IServiceProvider services)
    : IEntityRequestHandler<AttachmentFileByNameQuery<TEntity>, Attachment>
    where TEntity : class, IEntityAttachment<int, int, int, Attachment>
{
    protected IServiceProvider Services { get; } = services;

    public virtual async Task<Attachment?> Handle(AttachmentFileByNameQuery<TEntity> request, CancellationToken token = default)
    {
        var service = Services.GetRequiredEntityService<IEntityService<TEntity, int>>();
        // the lookup by name lists the link without its file content; Details reads the content
        var link = (await service.List(new { objectId = new[] { request.ObjectId }, fileName = request.FileName }, new PagingInfo { PageSize = 1 }, token))
            .FirstOrDefault();
        if (link == null)
        {
            return null;
        }
        return (await service.Details(link.Id, token))?.Attachment;
    }
}

internal static class AttachmentLinks
{
    /// <summary>A link with its attachment but without the file content.</summary>
    public static async Task<TEntity?> FetchLink<TEntity>(this IEntityService<TEntity, int> service, int id, CancellationToken token)
        where TEntity : class, IEntityAttachment<int, int, int, Attachment>
        => (await service.List(new { id }, new PagingInfo { PageSize = 1 }, token)).SingleOrDefault();

    public static EntityInputException<TEntity> NotALinkOfThisOwner<TEntity>()
        => new("Not a link of this owner.") { InputErrors = { ["objectId"] = "Not a link of this owner." } };
}
