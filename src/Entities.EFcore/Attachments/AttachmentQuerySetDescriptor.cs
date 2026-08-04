using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;

namespace Regira.Entities.EFcore.Attachments;

public interface IAttachmentQuerySetDescriptor
{
    string ObjectType { get; }
    IQueryable<IEntityAttachment<int, int, int, Attachment>> QuerySet { get; }
}
public class AttachmentQuerySetDescriptor<T> : IAttachmentQuerySetDescriptor
{
    public string ObjectType => typeof(T).Name;
    public IQueryable<IEntityAttachment<int, int, int, Attachment>> QuerySet { get; set; } = null!;
}