using Microsoft.EntityFrameworkCore;
using Regira.Entities.QueryBuilders.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Persons;

public class PersonIncludableQueryBuilder : IIncludableQueryBuilder<Person, int, PersonIncludes>
{
    public IQueryable<Person> AddIncludes(IQueryable<Person> query, PersonIncludes? includes = null)
    {
        if (includes.HasValue)
        {
            if (includes.Value.HasFlag(PersonIncludes.Supervisor))
            {
                query = query.Include(x => x.Supervisor);
            }
            if (includes.Value.HasFlag(PersonIncludes.Subordinates))
            {
                query = query.Include(x => x.Subordinates);
            }
            if (includes.Value.HasFlag(PersonIncludes.Departments))
            {
                query = query.Include(x => x.Departments);
            }
        }

        return query;
    }
}