using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Models.Abstractions;
using System.ComponentModel.DataAnnotations.Schema;

namespace Regira.Entities.Attachments.Models;

public class EntityAttachment : EntityAttachment<int, int, int, Attachment>, IEntityAttachment<int, int>, IEntityWithSerial;
public class EntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment> : IEntityAttachment<TKey, TObjectKey, TAttachmentKey, TAttachment>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    public TKey Id { get; set; } = default!;
    public TObjectKey ObjectId { get; set; } = default!;
    public TAttachmentKey AttachmentId { get; set; } = default!;
    [NotMapped]
    public string? ObjectType { get; set; }
    public int SortOrder { get; set; }

    public TAttachment? Attachment { get; set; }


    [NotMapped]
    public string? NewFileName { get; set; }
    [NotMapped]
    public string? NewContentType { get; set; }
    [NotMapped]
    public byte[]? NewBytes { get; set; }

    IAttachment? IEntityAttachment.Attachment
    {
        get => Attachment;
        set => Attachment = value as TAttachment;
    }
}