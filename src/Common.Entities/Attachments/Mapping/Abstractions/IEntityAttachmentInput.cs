namespace Regira.Entities.Attachments.Mapping.Abstractions;

public interface IEntityAttachmentInput : IEntityAttachmentInput<int, int>;
public interface IEntityAttachmentInput<TKey, TObjectId> : IEntityAttachmentInput<TKey, TObjectId, int>;
public interface IEntityAttachmentInput<TKey, TObjectId, TAttachmentId>
{
    TKey Id { get; set; }
    TObjectId ObjectId { get; set; }
    TAttachmentId AttachmentId { get; set; }

    string? NewFileName { get; set; }
    [Obsolete("Ignored: an attachment's content type follows its file name, so a client cannot choose the type its file is served as.")]
    string? NewContentType { get; set; }
}
