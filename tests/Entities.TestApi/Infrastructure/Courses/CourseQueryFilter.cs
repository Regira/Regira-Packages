using Regira.Entities.QueryBuilders.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Courses;

public class CourseQueryFilter : IFilteredQueryBuilder<Course, int, CourseSearchObject>
{
    public IQueryable<Course> Build(IQueryable<Course> query, CourseSearchObject? so)
    {
        if (so?.DepartmentId.HasValue == true)
        {
            query = query.Where(x => x.DepartmentId == so.DepartmentId);
        }

        return query;
    }
}