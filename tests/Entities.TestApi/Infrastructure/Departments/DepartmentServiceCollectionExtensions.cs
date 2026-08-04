using Microsoft.EntityFrameworkCore;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Departments;

public static class DepartmentServiceCollectionExtensions
{
    public static IEntityServiceCollection<TContext> AddDepartments<TContext>(this IEntityServiceCollection<TContext> services)
        where TContext : DbContext
    {
        services
            .For<Department>(e =>
            {
                e.UseMapping<DepartmentDto, DepartmentInputDto>();
                e.AddFilter<DepartmentMax10YearsOldQueryFilter>();
            });

        return services;
    }
}