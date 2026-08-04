using Regira.Entities.Services;
using Regira.Entities.Services.Abstractions;
using Testing.Library.Contoso;

namespace Entities.TestApi.Infrastructure.Persons;

public class PersonManager(IEntityRepository<Person, int, PersonSearchObject, PersonSortBy, PersonIncludes> repo)
    : EntityManager<Person, PersonSearchObject, PersonSortBy, PersonIncludes>(repo);