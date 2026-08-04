using AutoMapper;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Entities.Web.Attachments.Services;

namespace Regira.Entities.Mapping.AutoMapper;

public class AttachmentUriResolver<TEntity, TDto>(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    : AttachmentUriResolver<TEntity, int, int, int, Attachment, TDto>(linkGenerator, httpContextAccessor)
    where TEntity : IEntity<int>, IEntityAttachment<int, int, int, Attachment>;

public class AttachmentUriResolver<TEntity, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment, TDto>(LinkGenerator linkGenerator, IHttpContextAccessor httpContextAccessor)
    : AttachmentUriResolver<TEntity, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(linkGenerator, httpContextAccessor), IValueResolver<TEntity, TDto, string?>
    where TEntity : IEntity<TEntityAttachmentKey>, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    public string? Resolve(TEntity source, TDto destination, string? destMember, ResolutionContext context) 
        => base.Resolve(source);
}