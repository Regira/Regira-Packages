using Microsoft.EntityFrameworkCore;
using Regira.Entities.DependencyInjection.Attachments;
using Regira.Entities.DependencyInjection.ServiceCollections.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Persons;

public static class PersonServiceCollectionExtensions
{
    public static IEntityServiceCollection<TContext> AddPersons<TContext>(this IEntityServiceCollection<TContext> services)
        where TContext : DbContext
    {
        services
            .For<Person, PersonSearchObject, PersonSortBy, PersonIncludes>(e =>
            {
                e.Related(x => x.Departments);
                //e.Related(x => x.Subordinates);
                e.HasAttachments(x => x.Attachments);
                e.AddFilter<PersonQueryFilter>();
                e.AddIncludes<PersonIncludableQueryBuilder>();
                //e.SortBy<PersonSortQueryBuilder>();
                e.SortBy((query, sortBy) =>
                {
                    return sortBy switch
                    {
                        PersonSortBy.IdDesc => query.OrderByDescending(x => x.Id),
                        PersonSortBy.GivenName => query.OrderBy(x => x.GivenName),
                        PersonSortBy.LastName => query.OrderBy(x => x.LastName),
                        PersonSortBy.Title => query.OrderBy(x => x.LastName).ThenBy(x => x.GivenName),
                        _ => query.OrderBy(x => x.GivenName).ThenBy(x => x.LastName)
                    };
                });
                e.UseMapping<PersonDto, PersonInputDto>();
                e.AddNormalizer<PersonNormalizer>();
                e.HasManager<PersonManager>();
            });

        return services;
    }
}