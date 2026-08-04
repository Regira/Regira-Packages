using Bogus;

namespace TreeList.Testing.Infrastructure;

public class DataGenerator
{
    public static List<SimplePerson> GenerateSimplePersons(int n)
    {
        return new Faker<SimplePerson>()
            .RuleFor(x => x.Id, (f, x) => f.IndexGlobal + 1)
            .RuleFor(x => x.GivenName, (f, x) => f.Name.FirstName())
            .RuleFor(x => x.FamilyName, (f, x) => f.Name.LastName())
            .Generate(n);
    }

    public static List<Person> GeneratePersons(int n)
    {
        return new Faker<Person>()
            .RuleFor(x => x.Id, (f, x) => f.IndexGlobal + 1)
            .RuleFor(x => x.GivenName, (f, x) => f.Name.FirstName())
            .RuleFor(x => x.FamilyName, (f, x) => f.Name.LastName())
            .Generate(n);
    }
}