using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public static class EntityAttachmentQueryExtensions
{
    public static IQueryable<TEntityAttachment> Filter<TEntityAttachment>(this IQueryable<TEntityAttachment> query, EntityAttachmentSearchObject? aso)
        where TEntityAttachment : class, IEntityAttachment<int, int, int, Attachment>, IEntity<int>
    {
        if (aso != null)
        {
            if (aso.ObjectId?.Any() == true)
            {
                query = query.Where(x => aso.ObjectId.Contains(x.ObjectId));
            }

            if (!string.IsNullOrWhiteSpace(aso.FileName))
            {
                query = query.Where(x => x.Attachment!.FileName == aso.FileName);
            }
            if (!string.IsNullOrWhiteSpace(aso.Extension))
            {
                query = query.Where(x => EF.Functions.Like(x.Attachment!.FileName!, $"%{aso.Extension}"));
            }

            if (aso.MinSize.HasValue)
            {
                query = query.Where(x => x.Attachment!.Length >= aso.MinSize);
            }
            if (aso.MaxSize.HasValue)
            {
                query = query.Where(x => x.Attachment!.Length <= aso.MaxSize);
            }
        }

        return query;
    }
    /// <summary>
    /// Include the <see cref="IEntityAttachment">EntityAttachments</see> and it's underlying <see cref="IAttachment">Attachment</see>
    /// </summary>
    /// <typeparam name="THasEntityAttachments"></typeparam>
    /// <param name="query"></param>
    /// <returns></returns>
    public static IQueryable<THasEntityAttachments> IncludeEntityAttachments<THasEntityAttachments>(this IQueryable<THasEntityAttachments> query)
        where THasEntityAttachments : class, IHasAttachments, IEntity<int>
        => query.Include(item => item.Attachments!)
            .ThenInclude(a => a.Attachment);
}