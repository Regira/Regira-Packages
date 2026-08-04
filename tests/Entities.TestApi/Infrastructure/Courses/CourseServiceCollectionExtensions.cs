using Entities.TestApi.Infrastructure.Persons;
using Microsoft.EntityFrameworkCore;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Regira.Entities.EFcore.Attachments;
using Regira.Entities.QueryBuilders.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Courses;

public static class CourseServiceCollectionExtensions
{
    public static IEntityServiceCollection<TContext> AddCourses<TContext>(this IEntityServiceCollection<TContext> services)
        where TContext : DbContext
    {
        services
            .For<Course, int, CourseSearchObject>(e =>
            {
                e.Includes((query, _) => query.IncludeEntityAttachments());
                e.SortBy(query => query.OrderBy(x => x.Title));
                e.AddFilter<CourseQueryFilter>();
                e.UseMapping<CourseDto, CourseInputDto>();
                e.HasAttachments(
                    course => course.Attachments,
                    a => a.UseMapping<CourseAttachmentDto, CourseAttachmentInputDto>()
                );
                // extra person filter
                e.AddTransient<IFilteredQueryBuilder<Person, int, PersonSearchObject>, CoursePersonQueryFilter>();
            });

        return services;
    }
}