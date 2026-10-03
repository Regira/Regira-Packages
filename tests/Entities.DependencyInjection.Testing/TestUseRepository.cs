using Entities.DependencyInjection.Testing.Infrastructure;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.DependencyInjection.ServiceCollections;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestUseRepository
{
    private static readonly Type[] AppRepositories =
        [typeof(AppRepository<>), typeof(AppRepository<,>), typeof(AppRepository<,,>), typeof(AppRepository<,,,>), typeof(AppRepository<,,,,>)];

    [Test]
    public void For1_Uses_App_Repository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(AppRepositories))
            .For<Course>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityRepository<Course>>(), Is.TypeOf<AppRepository<Course>>());
        Assert.That(sp.GetService<IEntityRepository<Course, int>>(), Is.TypeOf<AppRepository<Course>>());
        Assert.That(sp.GetService<IEntityRepository<Course, int, SearchObject<int>>>(), Is.TypeOf<AppRepository<Course>>());
        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf<AppRepository<Course>>());
        Assert.That(sp.GetService<IEntityService<Course, int>>(), Is.TypeOf<AppRepository<Course>>());
        Assert.That(sp.GetService<IEntityService<Course, int, SearchObject<int>>>(), Is.TypeOf<AppRepository<Course>>());
    }

    [Test]
    public void For2_Uses_App_Repository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(AppRepositories))
            .For<Course, int>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityRepository<Course, int>>(), Is.TypeOf<AppRepository<Course, int>>());
        Assert.That(sp.GetService<IEntityRepository<Course, int, SearchObject<int>>>(), Is.TypeOf<AppRepository<Course, int>>());
        Assert.That(sp.GetService<IEntityService<Course, int>>(), Is.TypeOf<AppRepository<Course, int>>());
        Assert.That(sp.GetService<IEntityService<Course, int, SearchObject<int>>>(), Is.TypeOf<AppRepository<Course, int>>());
    }

    [Test]
    public void For3_Uses_App_Repository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(AppRepositories))
            .For<Course, int, CourseSearchObject>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityRepository<Course, int>>(), Is.TypeOf<AppRepository<Course, int, CourseSearchObject>>());
        Assert.That(sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>(), Is.TypeOf<AppRepository<Course, int, CourseSearchObject>>());
        Assert.That(sp.GetService<IEntityService<Course, int>>(), Is.TypeOf<AppRepository<Course, int, CourseSearchObject>>());
        Assert.That(sp.GetService<IEntityService<Course, int, CourseSearchObject>>(), Is.TypeOf<AppRepository<Course, int, CourseSearchObject>>());
    }

    [Test]
    public void For4_Uses_App_Repository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(AppRepositories))
            .For<Course, CourseSearchObject, CourseSortBy, CourseIncludes>()
            .BuildServiceProvider();

        var expected = typeof(AppRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>);
        Assert.That(sp.GetService<IEntityRepository<Course>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityRepository<Course, int>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityRepository<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course, int>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course, int, CourseSearchObject>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course, CourseSearchObject, CourseSortBy, CourseIncludes>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>(), Is.TypeOf(expected));
    }

    [Test]
    public void For5_Uses_App_Repository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(AppRepositories))
            .For<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>()
            .BuildServiceProvider();

        var expected = typeof(AppRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>);
        Assert.That(sp.GetService<IEntityRepository<Course, int>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityRepository<Course, int, CourseSearchObject>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityRepository<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course, int>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course, int, CourseSearchObject>>(), Is.TypeOf(expected));
        Assert.That(sp.GetService<IEntityService<Course, int, CourseSearchObject, CourseSortBy, CourseIncludes>>(), Is.TypeOf(expected));
    }

    [Test]
    public void Applies_Across_UseEntities_Calls()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities(o => o.UseRepository(typeof(AppRepository<>)));
        using var sp = services
            .UseEntities<ContosoContext>(o => o.UseDefaults())
            .For<Course>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf<AppRepository<Course>>());
    }

    [Test]
    public void HasRepository_Wins()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(AppRepositories))
            .For<Course>(e => e.HasRepository<CourseRepository1>())
            .For<Department>(e => e.HasRepository<EntityRepository<Department>>())
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityRepository<Course>>(), Is.TypeOf<CourseRepository1>());
        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf<CourseRepository1>());
        Assert.That(sp.GetService<IEntityRepository<Department>>(), Is.TypeOf<EntityRepository<Department>>());
        Assert.That(sp.GetService<IEntityService<Department>>(), Is.TypeOf<EntityRepository<Department>>());
    }

    [Test]
    public void Custom_EntityService_Keeps_App_Repository_Underneath()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(AppRepositories))
            .For<Course>(e => e.HasManager<CourseManager1>())
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf<CourseManager1>());
        Assert.That(sp.GetService<IEntityRepository<Course>>(), Is.TypeOf<AppRepository<Course>>());
    }

    [Test]
    public void Missing_Shape_Keeps_Default_And_Is_Recorded()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        using var sp = services
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseRepository(typeof(AppRepository<,>)))
            .For<Course>()
            .For<Department, int>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf<EntityRepository<Course>>());
        Assert.That(sp.GetService<IEntityService<Department, int>>(), Is.TypeOf<AppRepository<Department, int>>());
        var registry = sp.GetRequiredService<EntityRepositoryRegistry>();
        Assert.That(registry.EntitiesOnDefault, Is.EquivalentTo(new[] { (typeof(Course), 1) }));
    }

    [Test]
    public void Without_UseRepository_Records_Nothing()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults())
            .For<Course>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf<EntityRepository<Course>>());
        Assert.That(sp.GetService<EntityRepositoryRegistry>()?.EntitiesOnDefault ?? [], Is.Empty);
    }

    [TestCase(typeof(AppRepository<Course>), TestName = "Rejects_A_Closed_Type")]
    [TestCase(typeof(NotARepository<>), TestName = "Rejects_A_Type_That_Is_No_Repository")]
    public void Rejects_Invalid_Type(Type repositoryType)
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.UseEntities<ContosoContext>(o => o.UseRepository(repositoryType)));
    }

    [Test]
    public void Rejects_An_Entity_Outside_The_Type_Constraints()
    {
        var entities = new ServiceCollection()
            .UseEntities<ContosoContext>(o => o.UseRepository(typeof(AttachmentOwnerRepository<>)))
            .For<Course>();

        var ex = Assert.Throws<InvalidOperationException>(() => entities.For<Department>());
        Assert.That(ex!.Message, Does.Contain(nameof(Department)));
    }

    [Test]
    public void Rejects_A_Type_Missing_An_Interface_Of_The_Default()
    {
        var entities = new ServiceCollection()
            .UseEntities<ContosoContext>(o => o.UseRepository(typeof(IntKeyedRepository<>)));

        var ex = Assert.Throws<InvalidOperationException>(() => entities.For<Course>());
        Assert.That(ex!.Message, Does.Contain("IEntityRepository<Course>"));
    }
}
