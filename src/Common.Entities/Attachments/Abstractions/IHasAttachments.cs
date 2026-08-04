using Regira.Entities.Attachments.Models;

namespace Regira.Entities.Attachments.Abstractions;


public interface IHasAttachments
{
    ICollection<IEntityAttachment>? Attachments { get; set; }
    public bool? HasAttachment { get; set; }
}

public interface IHasAttachments<TEntityAttachment> : IHasAttachments<TEntityAttachment, int, int, int, Attachment>
    where TEntityAttachment : IEntityAttachment<int, int, int, Attachment>;

public interface IHasAttachments<TEntityAttachment, TKey, TObjectKey, TAttachmentKey, TAttachment>
    where TEntityAttachment : IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    ICollection<TEntityAttachment>? Attachments { get; set; }
    public bool? HasAttachment { get; set; }
}