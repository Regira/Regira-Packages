using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Keywords;
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
                // Same meaning as AttachmentFilteredQueryBuilder: an extension, not a suffix. The pattern is
                // anchored on the separating dot, so "pdf" cannot also match a file named "handbook-nopdf",
                // and the dot is supplied when the caller omits it, so "pdf", ".pdf" and "*.pdf" all mean the
                // same thing. Parsing strips the input wildcards first; re-parsing the dotted form is what
                // puts the store's wildcard into the pattern instead of hard-coding '%' here.
                // No DI here (a static extension), so the wildcards are QKeywordHelperOptions' defaults:
                // '*' in, '%' out. A consumer that configured different ones has to build this predicate
                // itself — EntityAttachmentFilteredQueryBuilder does take an IQKeywordHelper, but it
                // implements ObjectId and FileName only, so it is no substitute for this method.
                var qHelper = new QKeywordHelper();
                var extension = qHelper.ParseKeyword(aso.Extension).Trimmed;
                // A value of only wildcards trims to empty: anchoring that would match every dotted name for
                // no stated intent, so the clause is skipped and the remaining filters stand on their own.
                if (!string.IsNullOrEmpty(extension))
                {
                    var dotted = extension.StartsWith('.') ? extension : $".{extension}";
                    var pattern = qHelper.ParseKeyword(dotted).TrimmedEndsWith!;
                    query = query.Where(x => EF.Functions.Like(x.Attachment!.FileName!, pattern));
                }
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