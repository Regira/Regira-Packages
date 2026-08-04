using Microsoft.EntityFrameworkCore;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
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
            });

        return services;
    }
}