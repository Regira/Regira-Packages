using Microsoft.EntityFrameworkCore;
using Regira.Entities.QueryBuilders.Abstractions;
using Regira.Entities.Keywords.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Persons;

public class PersonQueryFilter(IQKeywordHelper queryHelper) : IFilteredQueryBuilder<Person, int, PersonSearchObject>
{
    public IQueryable<Person> Build(IQueryable<Person> query, PersonSearchObject? so)
    {
        if (so == null)
        {
            return query;
        }

        if (!string.IsNullOrWhiteSpace(so.LastName))
        {
            var name = queryHelper.ParseKeyword(so.LastName);
            query = name.HasWildcard
                ? query.Where(x => EF.Functions.Like(x.LastName!, name.Q!))
                : query.Where(x => x.LastName == name.Keyword);
        }
        if (!string.IsNullOrWhiteSpace(so.GivenName))
        {
            var name = queryHelper.ParseKeyword(so.GivenName);
            query = name.HasWildcard
                ? query.Where(x => EF.Functions.Like(x.GivenName!, name.Q!))
                : query.Where(x => x.GivenName == name.Keyword);
        }

        if (!string.IsNullOrWhiteSpace(so.Phone))
        {
            var phone = queryHelper.ParseKeyword(so.Phone);
            query = phone.HasWildcard
                ? query.Where(x => EF.Functions.Like(x.Phone!, phone.Q!))
                : query.Where(x => x.Phone == phone.Keyword);
        }

        return query;
    }
}