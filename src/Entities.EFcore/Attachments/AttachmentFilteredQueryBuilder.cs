using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Keywords;
using Regira.Entities.Keywords.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public class AttachmentFilteredQueryBuilder(IQKeywordHelper? qHelper = null) : AttachmentFilteredQueryBuilder<Attachment, int, AttachmentSearchObject>(qHelper);

public class AttachmentFilteredQueryBuilder<TAttachment, TKey, TAttachmentSearchObject>(IQKeywordHelper? qHelper = null) : IFilteredQueryBuilder<TAttachment, TKey, TAttachmentSearchObject>
    where TAttachment : IAttachment<TKey>
    where TAttachmentSearchObject : AttachmentSearchObject<TKey>
{
    protected IQKeywordHelper QHelper { get; } = qHelper ?? new QKeywordHelper();

    public IQueryable<TAttachment> Build(IQueryable<TAttachment> query, TAttachmentSearchObject? so)
    {
        if (!string.IsNullOrWhiteSpace(so?.FileName))
        {
            var kw = QHelper.ParseKeyword(so.FileName);
            query = kw.HasWildcard
                ? query.Where(x => EF.Functions.Like(x.FileName!, kw.Q!))
                : query.Where(x => x.FileName == so.FileName);
        }

        if (!string.IsNullOrWhiteSpace(so?.Extension))
        {
            query = query.Where(x => EF.Functions.Like(x.FileName!, $"*{so.Extension}"));
        }

        if (so?.MinSize.HasValue == true)
        {
            query = query.Where(x => x.Length >= so.MinSize);
        }
        if (so?.MaxSize.HasValue == true)
        {
            query = query.Where(x => x.Length <= so.MaxSize);
        }

        return query;
    }
}