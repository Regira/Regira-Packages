using Entities.Testing.Infrastructure.Normalizers;
using Entities.Testing.Infrastructure.Primers;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Normalizers;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Normalizing;
using Regira.Entities.Normalizing.Abstractions;
using Regira.Entities.EFcore.Primers;
using Regira.Entities.EFcore.Primers.Abstractions;
using Regira.Entities.Models.Abstractions;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing;

[TestFixture]
internal class NormalizingTests
{
    private SqliteConnection _connection = null!;
    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }
    [TearDown]
    public void TearDown()
    {
        _connection.Close();
    }

    [Test]
    public void Find_Normalizers_For_Entity()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ContosoContext>(db => db.UseSqlite(_connection));
        var serviceProvider = services.BuildServiceProvider();

        using var spScope = serviceProvider.CreateScope();
        var sp = spScope.ServiceProvider;

        var container = new EntityNormalizerContainer(sp);
        container.Register<Person>(_ => new PersonNormalizer(null));
        container.Register<IHasCourses>((_) => new ItemWithCoursesNormalizer(null));
        container.Register<Department>(p => new DepartmentAdministratorNormalizer(p.GetRequiredService<ContosoContext>(), new ItemWithCoursesNormalizer(null), null));

        var personNormalizers = container.FindAll<Person>();
        Assert.That(personNormalizers, Is.Not.Empty);
        Assert.That(personNormalizers.Count, Is.EqualTo(1));

        var instructorNormalizers = container.FindAll<Instructor>();
        Assert.That(instructorNormalizers, Is.Not.Empty);
        Assert.That(instructorNormalizers.Count, Is.EqualTo(2));
    }
    [Test]
    public void Find_Exclusive_Normalizers()
    {
        IServiceCollection services = new ServiceCollection();
        services.AddDbContext<ContosoContext>(db => db.UseSqlite(_connection));
        var serviceProvider = services.BuildServiceProvider();

        using var spScope = serviceProvider.CreateScope();
        var sp = spScope.ServiceProvider;

        var container = new EntityNormalizerContainer(sp);
        container.Register<Person>(_ => new PersonNormalizer(null));
        container.Register<IHasCourses>((_) => new ItemWithCoursesNormalizer(null));
        container.Register<Department>(p => new DepartmentAdministratorNormalizer(p.GetRequiredService<ContosoContext>(), new ItemWithCoursesNormalizer(null), null));

        var departmentNormalizer = container.FindAll<Department>().ToArray();
        Assert.That(departmentNormalizer, Is.Not.Empty);
        Assert.That(departmentNormalizer.Count, Is.EqualTo(1));
    }
    [Test]
    public void Extract_Normalizers_By_Container()
    {
        IServiceCollection services = new ServiceCollection();
        services
            .AddDbContext<ContosoContext>(db => db.UseSqlite(_connection))
            .UseEntities<ContosoContext>(e => e.AddDefaultEntityNormalizer())
            .AddTransient<IEntityNormalizer<Person>, PersonNormalizer>()
            .AddTransient<IEntityNormalizer<IHasCourses>, ItemWithCoursesNormalizer>()
            .AddTransient<IEntityNormalizer<Department>, DepartmentAdministratorNormalizer>()
            .AddObjectNormalizingContainer();
        var serviceProvider = services.BuildServiceProvider();

        using var spScope = serviceProvider.CreateScope();
        var sp = spScope.ServiceProvider;

        var container = sp.GetRequiredService<EntityNormalizerContainer>();

        var personNormalizers = container.FindAll<Person>().ToArray();
        Assert.That(personNormalizers, Is.Not.Empty);
        Assert.That(personNormalizers.Count, Is.EqualTo(2));

        var instructorNormalizers = container.FindAll<Instructor>().ToArray();
        Assert.That(instructorNormalizers, Is.Not.Empty);
        Assert.That(instructorNormalizers.Count, Is.EqualTo(3));

        var departmentNormalizer = container.FindAll<Department>().ToArray();
        Assert.That(departmentNormalizer, Is.Not.Empty);
        // exclusive
        Assert.That(departmentNormalizer.Count, Is.EqualTo(1));
    }

    [Test]
    public async Task Apply_DbContext_Normalization()
    {
        IServiceCollection services = new ServiceCollection();
        services
            .AddDbContext<ContosoContext>(db => db.UseSqlite(_connection))
            .AddTransient<IEntityNormalizer<IEntity>, DefaultEntityNormalizer>()
            .AddObjectNormalizingContainer();
        var serviceProvider = services.BuildServiceProvider();

        using var spScope = serviceProvider.CreateScope();
        var sp = spScope.ServiceProvider;

        var dbContext = sp.GetRequiredService<ContosoContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var (people, _, _) = TestData.Generate();
        var john = people.John;
        var jane = people.Jane;
        var francois = people.Francois;
        var bob = people.Bob;
        var bill = people.Bill;

        dbContext.AddRange(john, jane, francois, bob, bill);

        await dbContext.ApplyNormalizers();

        Assert.That(john.NormalizedTitle, Is.EqualTo("Doe John"));
        Assert.That(john.NormalizedContent, Is.EqualTo("John Doe This is a male test person johndoeemailcom"));

        Assert.That(jane.NormalizedTitle, Is.EqualTo("Doe Jane"));
        Assert.That(jane.NormalizedContent, Is.EqualTo("Jane Doe This is a female test person 001 234 567 890"));

        Assert.That(francois.NormalizedTitle, Is.EqualTo("Du sacre Coeur Francois"));
        Assert.That(francois.NormalizedContent, Is.EqualTo("Francois Du sacre Coeur Le poete parisien du xiiie siecle Rutebeuf se fait gravement l echo de la faiblesse humaine de l incertitude et de la pauvrete a l oppose des valeurs courtoises Creme brulee"));

        Assert.That(bob.NormalizedTitle, Is.EqualTo("Kennedy Robert"));
        Assert.That(bob.NormalizedContent, Is.EqualTo("Robert Kennedy He s an American politician and lawyer known for his roles as US Attorney General and Senator his advocacy for civil rights and social justice and his tragic assassination in 1968 while campaigning for the presidency"));

        Assert.That(bill.NormalizedTitle, Is.EqualTo("Nixon Richard"));
        Assert.That(bill.NormalizedContent, Is.EqualTo("Richard Nixon He s the 37th President of the United States remembered for his foreign policy achievements and his involvement in the Watergate scandal which led to his resignation in 1974"));
    }
    [Test]
    public async Task Apply_Normalizing_As_Primer_Interceptor()
    {
        IServiceCollection services = new ServiceCollection();
        services
            .AddDbContext<ContosoContext>((sp, db) =>
            {
                db.UseSqlite(_connection);
                db.AddInterceptors(new EntityPrimerContainerInterceptor(sp));
            })
            .AddTransient<IEntityPrimer, NormalizingPrimer>();
        var serviceProvider = services.BuildServiceProvider();

        using var spScope = serviceProvider.CreateScope();
        var sp = spScope.ServiceProvider;

        var dbContext = sp.GetRequiredService<ContosoContext>();
        await dbContext.Database.EnsureCreatedAsync();

        var (people, _, _) = TestData.Generate();
        var john = people.John;
        var jane = people.Jane;
        var francois = people.Francois;
        var bob = people.Bob;
        var bill = people.Bill;

        dbContext.AddRange(john, jane, francois, bob, bill);

        await dbContext.SaveChangesAsync();

        Assert.That(john.NormalizedTitle, Is.EqualTo("Doe John"));
        Assert.That(john.NormalizedContent, Is.EqualTo("John Doe This is a male test person johndoeemailcom"));

        Assert.That(jane.NormalizedTitle, Is.EqualTo("Doe Jane"));
        Assert.That(jane.NormalizedContent, Is.EqualTo("Jane Doe This is a female test person 001 234 567 890"));

        Assert.That(francois.NormalizedTitle, Is.EqualTo("Du sacre Coeur Francois"));
        Assert.That(francois.NormalizedContent, Is.EqualTo("Francois Du sacre Coeur Le poete parisien du xiiie siecle Rutebeuf se fait gravement l echo de la faiblesse humaine de l incertitude et de la pauvrete a l oppose des valeurs courtoises Creme brulee"));

        Assert.That(bob.NormalizedTitle, Is.EqualTo("Kennedy Robert"));
        Assert.That(bob.NormalizedContent, Is.EqualTo("Robert Kennedy He s an American politician and lawyer known for his roles as US Attorney General and Senator his advocacy for civil rights and social justice and his tragic assassination in 1968 while campaigning for the presidency"));

        Assert.That(bill.NormalizedTitle, Is.EqualTo("Nixon Richard"));
        Assert.That(bill.NormalizedContent, Is.EqualTo("Richard Nixon He s the 37th President of the United States remembered for his foreign policy achievements and his involvement in the Watergate scandal which led to his resignation in 1974"));
    }
}