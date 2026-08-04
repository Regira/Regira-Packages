using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Attachments.Abstractions;

public interface IAttachmentUriResolver<in TEntity> 
    where TEntity : IEntity, IEntityAttachment
{
    string? Resolve(TEntity source);
}