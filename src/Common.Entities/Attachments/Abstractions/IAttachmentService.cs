using Regira.Entities.Attachments.Models;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.Attachments.Abstractions;

public interface IAttachmentService : IAttachmentService<Attachment, int, AttachmentSearchObject>;
public interface IAttachmentService<TAttachment, TKey> : IAttachmentService<TAttachment, TKey, AttachmentSearchObject<TKey>>
    where TAttachment : class, IAttachment<TKey>, new();
public interface IAttachmentService<TAttachment, TKey, in TAttachmentSearchObject> : IEntityService<TAttachment, TKey, TAttachmentSearchObject>
    where TAttachment : class, IAttachment<TKey>, new()
    where TAttachmentSearchObject : AttachmentSearchObject<TKey>, new();