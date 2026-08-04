using Microsoft.EntityFrameworkCore;
using Regira.Entities.QueryBuilders.Abstractions;
using Testing.Library.Contoso;

namespace Entities.DependencyInjection.Testing.Infrastructure;

public class CourseIncludingQueryBuilder
    : IIncludableQueryBuilder<Course, int, CourseIncludes>
{
    public IQueryable<Course> AddIncludes(IQueryable<Course> query, CourseIncludes? includes = null)
    {
        return query.Include(x => x.Instructors);
    }
}