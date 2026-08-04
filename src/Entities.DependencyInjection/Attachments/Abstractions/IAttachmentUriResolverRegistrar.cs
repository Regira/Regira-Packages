using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.Attachments.Abstractions;

/// <summary>
/// Registers an <see cref="IAttachmentUriResolver{TEntityAttachment}"/> implementation for an entity attachment type.
/// Allows the (ASP.NET Core based) URI resolver to be plugged in from the web layer without the DI layer
/// referencing <c>Entities.Web</c>. Install one via the web-side <c>UseAttachmentUris()</c> extension.
/// </summary>
public interface IAttachmentUriResolverRegistrar
{
    void Register<TEntityAttachment, TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>(IServiceCollection services)
        where TEntityAttachment : class, IEntity<TEntityAttachmentKey>, IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>
        where TAttachment : class, IAttachment<TAttachmentKey>, new();
}
