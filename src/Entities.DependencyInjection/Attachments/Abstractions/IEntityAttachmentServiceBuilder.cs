using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.DependencyInjection.Attachments.Abstractions;

public interface IEntityAttachmentServiceBuilder<TEntity, TEntityAttachment> : IEntityAttachmentServiceBuilder<TEntity, int, TEntityAttachment, int, int, Attachment>
    where TEntity : class, IEntity<int>, IHasAttachments<TEntityAttachment>
    where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>;
// ReSharper disable once UnusedTypeParameter
public interface IEntityAttachmentServiceBuilder<TEntity, TKey, TEntityAttachment, TEntityAttachmentKey, TAttachmentKey, TAttachment>
    where TEntity : class, IEntity<TKey>, IHasAttachments<TEntityAttachment, TEntityAttachmentKey, TKey, TAttachmentKey, TAttachment>
    where TEntityAttachment : class, IEntityAttachment<TEntityAttachmentKey, TKey, TAttachmentKey, TAttachment>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    IServiceCollection Services { get; }
}
