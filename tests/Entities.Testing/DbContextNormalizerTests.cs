using Entities.Testing.Infrastructure.Normalizers;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Normalizers;
using Regira.Entities.DependencyInjection.QueryBuilders;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections.Models;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.EFcore.QueryBuilders.GlobalFilterBuilders;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Regira.Normalizing.Models;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing;

[TestFixture]
[Parallelizable(ParallelScope.None)]
internal class DbContextNormalizerTests
{
    [Test]
    public async Task Test_Normalizer_Options()
    {
        var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>(db => db.UseSqlite("DataSource=:memory:")) // UseDefaults() auto-wires the interceptors
            .UseEntities<ContosoContext>(e =>
            {
                e.UseDefaults(d =>
                {
                    d.ConfigureNormalizing(o => o.Transform = TextTransform.ToUpperCase);
                });
                e.AddGlobalFilterQueryBuilder<FilterHasNormalizedContentQueryBuilder>();
            })
            .For<Person>(e =>
            {
                e.AddNormalizer<PersonNormalizer>();
            })
            .BuildServiceProvider();

        var (people, _, _) = TestData.Generate();

        var dbContext = sp.GetRequiredService<ContosoContext>();
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Persons.AddRange(people.All);

        await dbContext.SaveChangesAsync();

        var personService = sp.GetRequiredService<IEntityService<Person>>();

        foreach (var person in people.All)
        {
            var personsFound = await personService.List(new SearchObject { Q = $"{person.GivenName} {person.LastName}".ToLower() });
            Assert.That(personsFound.First().Id, Is.EqualTo(person.Id));
            Assert.That(person.NormalizedContent, Is.EqualTo(person.NormalizedContent?.ToUpper()));
        }

        await dbContext.Database.CloseConnectionAsync();
    }

    [Test]
    public async Task Test_Order_Of_Registration()
    {
        var sp = new ServiceCollection()
             .AddDbContext<ContosoContext>(db => db.UseSqlite("DataSource=:memory:"))
             .UseEntities<ContosoContext>(e => e.WireDbContext(DbContextWiring.NormalizerInterceptors))
             .For<Department>(e =>
             {
                 e.AddNormalizer<Department1Normalizer>();
                 e.AddNormalizer<Department2Normalizer>();
             })
             .BuildServiceProvider();

        var (_, departments, _) = TestData.Generate();
        var item = departments.Biology;

        var dbContext = sp.GetRequiredService<ContosoContext>();
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        dbContext.Departments.Add(item);
        await dbContext.SaveChangesAsync();
        await dbContext.Database.CloseConnectionAsync();

        Assert.That(item.NormalizedContent, Is.EqualTo("DEPARTMENT_2 DEPARTMENT_1"));
    }

    [Test]
    public async Task Apply_Normalizing_Interceptors()
    {
        IServiceCollection services = new ServiceCollection();
        var sp = services
            .AddDbContext<ContosoContext>(db => db.UseSqlite("DataSource=:memory:"))
            .UseEntities<ContosoContext>(e =>
            {
                e.WireDbContext(DbContextWiring.NormalizerInterceptors);
                e.AddDefaultEntityNormalizer();
                e.AddNormalizer<Person, PersonNormalizer>();
                e.AddNormalizer<Instructor, InstructorNormalizer>();
            })
            .BuildServiceProvider();

        var dbContext = sp.GetRequiredService<ContosoContext>();
        await dbContext.Database.OpenConnectionAsync();
        await dbContext.Database.EnsureCreatedAsync();

        var (people, _, courses) = TestData.Generate();
        var john = people.John;
        var jane = people.Jane;
        var francois = people.Francois;
        var bob = people.Bob;
        var bill = people.Bill;

        bob.Courses = courses.All.Where(x => x.Instructors!.Contains(people.Bob)).ToList();
        bill.Courses = courses.All.Where(x => x.Instructors!.Contains(people.Bill)).ToList();

        dbContext.AddRange(john, jane, francois, bob, bill);

        await dbContext.SaveChangesAsync();
        await dbContext.Database.CloseConnectionAsync();

        Assert.That(john.NormalizedTitle, Is.EqualTo("Doe John"));
        Assert.That(john.NormalizedContent, Is.EqualTo("PERSON John Doe This is a male test person johndoeemailcom"));

        Assert.That(jane.NormalizedTitle, Is.EqualTo("Doe Jane"));
        Assert.That(jane.NormalizedContent, Is.EqualTo("PERSON Jane Doe This is a female test person 001 234 567 890"));

        Assert.That(francois.NormalizedTitle, Is.EqualTo("Du sacre Coeur Francois"));
        Assert.That(francois.NormalizedContent, Is.EqualTo("PERSON Francois Du sacre Coeur Le poete parisien du xiiie siecle Rutebeuf se fait gravement l echo de la faiblesse humaine de l incertitude et de la pauvrete a l oppose des valeurs courtoises Creme brulee"));

        Assert.That(bob.NormalizedTitle, Is.EqualTo("Kennedy Robert"));
        Assert.That(bob.NormalizedContent, Is.EqualTo("PERSON Robert Kennedy He s an American politician and lawyer known for his roles as US Attorney General and Senator his advocacy for civil rights and social justice and his tragic assassination in 1968 while campaigning for the presidency INSTRUCTING 6 COURSES"));

        Assert.That(bill.NormalizedTitle, Is.EqualTo("Nixon Richard"));
        Assert.That(bill.NormalizedContent, Is.EqualTo("PERSON Richard Nixon He s the 37th President of the United States remembered for his foreign policy achievements and his involvement in the Watergate scandal which led to his resignation in 1974 INSTRUCTING 6 COURSES"));
    }
}