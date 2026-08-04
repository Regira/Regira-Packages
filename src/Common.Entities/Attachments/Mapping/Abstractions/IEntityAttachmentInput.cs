namespace Regira.Entities.Attachments.Mapping.Abstractions;

public interface IEntityAttachmentInput : IEntityAttachmentInput<int, int>;
public interface IEntityAttachmentInput<TKey, TObjectId> : IEntityAttachmentInput<TKey, TObjectId, int>;
public interface IEntityAttachmentInput<TKey, TObjectId, TAttachmentId>
{
    TKey Id { get; set; }
    TObjectId ObjectId { get; set; }
    TAttachmentId AttachmentId { get; set; }

    string? NewFileName { get; set; }
    string? NewContentType { get; set; }
}
