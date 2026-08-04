using Microsoft.EntityFrameworkCore;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Keywords;
using Regira.Entities.Keywords.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.EFcore.Attachments;

public class EntityAttachmentFilteredQueryBuilder<TEntityAttachment, TSearchObject>(IQKeywordHelper? qHelper = null)
    : EntityAttachmentFilteredQueryBuilder<int, TEntityAttachment, int, TSearchObject, int, Attachment>(qHelper)
    where TEntityAttachment : IEntityAttachment<int, int, int, Attachment>, IEntity<int>
    where TSearchObject : class, IEntityAttachmentSearchObject<int, int>;
public class EntityAttachmentFilteredQueryBuilder<TObjectKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>(IQKeywordHelper? qHelper = null)
    : IFilteredQueryBuilder<TEntityAttachment, TEntityAttachmentKey, TSearchObject>
    where TEntityAttachment : IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>, IEntity<TEntityAttachmentKey>
    where TSearchObject : class, IEntityAttachmentSearchObject<TEntityAttachmentKey, TObjectKey>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    protected internal IQKeywordHelper QHelper = qHelper ?? new QKeywordHelper();

    public IQueryable<TEntityAttachment> Build(IQueryable<TEntityAttachment> query, TSearchObject? so)
    {
        if (so != null)
        {
            if (so.ObjectId?.Any() == true)
            {
                query = query.Where(x => so.ObjectId.Contains(x.ObjectId));
            }

            if (!string.IsNullOrWhiteSpace(so.FileName))
            {
                var kw = QHelper.ParseKeyword(so.FileName);
                query = kw.HasWildcard
                    ? query.Where(x => EF.Functions.Like(x.Attachment!.FileName!, kw.Q!))
                    : query.Where(x => x.Attachment!.FileName == so.FileName);
            }
        }
        return query;
    }
}


public class EntityAttachmentQueryFilter<TObjectKey, TEntityAttachment, TEntityAttachmentKey, TSearchObject, TAttachmentKey, TAttachment>(Func<IQueryable<TEntityAttachment>, TSearchObject?, IQueryable<TEntityAttachment>> filterFunc) 
    : IFilteredQueryBuilder<TEntityAttachment, TEntityAttachmentKey, TSearchObject>
    where TEntityAttachment : IEntityAttachment<TEntityAttachmentKey, TObjectKey, TAttachmentKey, TAttachment>, IEntity<TEntityAttachmentKey>
    where TSearchObject : class, IEntityAttachmentSearchObject<TEntityAttachmentKey, TObjectKey>
    where TAttachment : class, IAttachment<TAttachmentKey>, new()
{
    public IQueryable<TEntityAttachment> Build(IQueryable<TEntityAttachment> query, TSearchObject? so)
        => filterFunc(query, so);
}