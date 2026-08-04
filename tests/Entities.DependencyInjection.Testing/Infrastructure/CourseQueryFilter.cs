using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Models;
using Testing.Library.Contoso;

namespace Entities.DependencyInjection.Testing.Infrastructure;

public class CourseQueryFilter1 : IFilteredQueryBuilder<Course, int, SearchObject<int>>
{
    public IQueryable<Course> Build(IQueryable<Course> query, SearchObject<int>? so)
    {
        if (!string.IsNullOrWhiteSpace(so?.Q))
        {
            query = query.Where(x => x.Title!.Contains(so.Q));
        }

        return query;
    }
}

public class CourseQueryFilter3 : IFilteredQueryBuilder<Course, int, CourseSearchObject>
{
    public IQueryable<Course> Build(IQueryable<Course> query, CourseSearchObject? so)
    {
        if (so?.DepartmentId is not null)
        {
            query = query.Where(x => x.DepartmentId == so.DepartmentId);
        }
        return query;
    }
}