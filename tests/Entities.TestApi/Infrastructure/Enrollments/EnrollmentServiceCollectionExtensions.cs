using Microsoft.EntityFrameworkCore;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Regira.Entities.Web.Endpoints;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Enrollments;

public static class EnrollmentServiceCollectionExtensions
{
    public static IEntityServiceCollection<TContext> AddEnrollments<TContext>(this IEntityServiceCollection<TContext> services)
        where TContext : DbContext
    {
        services
            .For<Enrollment>(e =>
            {
                e.UseQueryBuilder<EnrollmentQueryBuilder>();
                // no controller and no DTOs: kept off the mapped endpoints, which would otherwise refuse it at startup
                e.Endpoints(o => o.Disable());
            });

        return services;
    }
}