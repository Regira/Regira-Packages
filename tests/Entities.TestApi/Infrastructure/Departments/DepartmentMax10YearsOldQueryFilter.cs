using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Departments;

public class DepartmentMax10YearsOldQueryFilter : FilteredQueryBuilderBase<Department>
{
    public override IQueryable<Department> Build(IQueryable<Department> query, SearchObject<int>? so)
    {
        if (so != null)
        {
            query = query.Where(x => x.Created > DateTime.Today.AddYears(-10));
        }

        return query;
    }
}