using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;

namespace Regira.Entities.EFcore.Attachments;

public static class QuerySetDescriptorExtensions
{
    public static IAttachmentQuerySetDescriptor ToDescriptor<T>(this IQueryable<IEntityAttachment<int, int, int, Attachment>> query)
        => new AttachmentQuerySetDescriptor<T> { QuerySet = query };
    public static IQueryable<IEntityAttachment<int, int, int, Attachment>> ToEntityAttachments(this IAttachmentQuerySetDescriptor querySetDescriptor)
        => querySetDescriptor.QuerySet.Select(x => new EntityAttachment
        {
            Id = x.Id,
            AttachmentId = x.AttachmentId,
            ObjectId = x.ObjectId,
            SortOrder = x.SortOrder,
            ObjectType = querySetDescriptor.ObjectType,
            Attachment = x.Attachment
        });
    public static IQueryable<IEntityAttachment<int, int, int, Attachment>> ConcatAll(this IList<IAttachmentQuerySetDescriptor> sets)
    {
        IQueryable<IEntityAttachment<int, int, int, Attachment>> query = sets.First().ToEntityAttachments();
        foreach (var set in sets.Skip(1))
        {
            query = query.Concat(set.ToEntityAttachments());
        }

        return query;
    }
}