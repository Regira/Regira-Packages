using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models.Abstractions;

namespace Entities.DependencyInjection.Testing.Infrastructure;

public class HasAttachmentGlobalQueryFilter : GlobalFilteredQueryBuilderBase<IHasAttachments>
{
    public override IQueryable<IHasAttachments> Build(IQueryable<IHasAttachments> query, ISearchObject<int>? so)
    {
        if (so?.Q == "HasAttachments")
        {
            query = query.Where(x => x.HasAttachment == true);
        }

        return query;
    }
}