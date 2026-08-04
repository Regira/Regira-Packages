using Regira.Entities.Attachments.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Attachments.Abstractions;

public interface IEntityAttachment
{
    string? ObjectType { get; }

    string? NewFileName { get; set; }
    string? NewContentType { get; set; }
    byte[]? NewBytes { get; set; }
    IAttachment? Attachment { get; set; }
}
public interface IEntityAttachment<TKey, TObjectKey> : IEntityAttachment<TKey, TObjectKey, int, Attachment>;
public interface IEntityAttachment<TKey, TObjectKey, TAttachmentKey> : IEntityAttachment<TKey, TObjectKey, TAttachmentKey, Attachment<TAttachmentKey>>;
public interface IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment> : IEntity<TKey>, IHasObjectId<TObjectKey>, IEntityAttachment, ISortable
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    TAttachmentKey AttachmentId { get; set; }

    new TAttachment? Attachment { get; set; }
}