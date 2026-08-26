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
                ? query.Where(x => EF.Functions.Like(x.FileName!, kw.TrimmedQ!))
                : query.Where(x => x.FileName == so.FileName);
        }

        if (!string.IsNullOrWhiteSpace(so?.Extension))
        {
            // An extension, not a suffix: the pattern is anchored on the separating dot, so "pdf" cannot also
            // match a file named "handbook-nopdf". The dot is supplied when the caller omits it, so "pdf",
            // ".pdf" and the wildcard spelling "*.pdf" all mean the same thing. Parsing first strips the
            // input wildcards; re-parsing the dotted form is what builds the pattern with the configured
            // wildcard output rather than a hard-coded '%'.
            var extension = QHelper.ParseKeyword(so.Extension).Trimmed;
            // A value of only wildcards trims to empty: anchoring that would match every dotted name for no
            // stated intent, so the clause is skipped and the remaining filters stand on their own.
            if (!string.IsNullOrEmpty(extension))
            {
                var dotted = extension.StartsWith('.') ? extension : $".{extension}";
                var pattern = QHelper.ParseKeyword(dotted).TrimmedEndsWith!;
                query = query.Where(x => EF.Functions.Like(x.FileName!, pattern));
            }
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