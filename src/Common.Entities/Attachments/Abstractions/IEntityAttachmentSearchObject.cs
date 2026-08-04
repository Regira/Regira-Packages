namespace Regira.Entities.Attachments.Abstractions;

public interface IEntityAttachmentSearchObject;
public interface IEntityAttachmentSearchObject<TKey, TObjectKey> : IEntityAttachmentSearchObject, IAttachmentSearchObject<TKey>
{
    ICollection<TObjectKey>? ObjectId { get; set; }
}