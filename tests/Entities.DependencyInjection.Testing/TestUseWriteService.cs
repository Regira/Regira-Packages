using Entities.DependencyInjection.Testing.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.EFcore.Services;
using Regira.Entities.Services.Abstractions;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.DependencyInjection.Testing;

[TestFixture]
public class TestUseWriteService
{
    [Test]
    public void Serves_Every_For_Shape()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseWriteService(typeof(AppWriteService<,,>)))
            .For<Course>()
            .For<Department, int>()
            .For<Student, int, CourseSearchObject>()
            .For<Instructor, CourseSearchObject, CourseSortBy, CourseIncludes>()
            .For<Person, int, CourseSearchObject, CourseSortBy, CourseIncludes>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityWriteService<Course, int>>(), Is.TypeOf<AppWriteService<ContosoContext, Course, int>>());
        Assert.That(sp.GetService<IEntityWriteService<Department, int>>(), Is.TypeOf<AppWriteService<ContosoContext, Department, int>>());
        Assert.That(sp.GetService<IEntityWriteService<Student, int>>(), Is.TypeOf<AppWriteService<ContosoContext, Student, int>>());
        Assert.That(sp.GetService<IEntityWriteService<Instructor, int>>(), Is.TypeOf<AppWriteService<ContosoContext, Instructor, int>>());
        Assert.That(sp.GetService<IEntityWriteService<Person, int>>(), Is.TypeOf<AppWriteService<ContosoContext, Person, int>>());
    }

    [Test]
    public void Composes_With_UseRepository()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults()
                .UseRepository(typeof(AppRepository<>))
                .UseWriteService(typeof(AppWriteService<,,>)))
            .For<Course>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityService<Course>>(), Is.TypeOf<AppRepository<Course>>());
        Assert.That(sp.GetService<IEntityWriteService<Course, int>>(), Is.TypeOf<AppWriteService<ContosoContext, Course, int>>());
    }

    [Test]
    public void Applies_Across_UseEntities_Calls()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities(o => o.UseWriteService(typeof(AppWriteService<,,>)));
        using var sp = services
            .UseEntities<ContosoContext>(o => o.UseDefaults())
            .For<Course>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityWriteService<Course, int>>(), Is.TypeOf<AppWriteService<ContosoContext, Course, int>>());
    }

    [Test]
    public void UseWriteService_Per_Entity_Wins()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults().UseWriteService(typeof(AppWriteService<,,>)))
            .For<Course>(e => e.UseWriteService<EntityWriteService<ContosoContext, Course>>())
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityWriteService<Course, int>>(), Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
    }

    [Test]
    public void Without_UseWriteService_Keeps_EntityWriteService()
    {
        using var sp = new ServiceCollection()
            .AddDbContext<ContosoContext>()
            .UseEntities<ContosoContext>(o => o.UseDefaults())
            .For<Course>()
            .For<Department, int>()
            .BuildServiceProvider();

        Assert.That(sp.GetService<IEntityWriteService<Course, int>>(), Is.TypeOf<EntityWriteService<ContosoContext, Course>>());
        Assert.That(sp.GetService<IEntityWriteService<Department, int>>(), Is.TypeOf<EntityWriteService<ContosoContext, Department, int>>());
    }

    [TestCase(typeof(AppWriteService<ContosoContext, Course, int>), TestName = "Rejects_A_Closed_Write_Service")]
    [TestCase(typeof(IntKeyedWriteService<,>), TestName = "Rejects_A_Write_Service_Without_3_Type_Parameters")]
    [TestCase(typeof(AppRepository<,,>), TestName = "Rejects_A_Type_That_Is_No_Write_Service")]
    public void Rejects_Invalid_Type(Type writeServiceType)
    {
        var services = new ServiceCollection();
        Assert.Throws<ArgumentException>(() => services.UseEntities<ContosoContext>(o => o.UseWriteService(writeServiceType)));
    }

    [Test]
    public void Rejects_An_Entity_Outside_The_Type_Constraints()
    {
        var entities = new ServiceCollection()
            .UseEntities<ContosoContext>(o => o.UseWriteService(typeof(AttachmentOwnerWriteService<,,>)))
            .For<Course>();

        var ex = Assert.Throws<InvalidOperationException>(() => entities.For<Department>());
        Assert.That(ex!.Message, Does.Contain(nameof(Department)));
    }

    [Test]
    public void Rejects_Type_Parameters_Out_Of_Order()
    {
        var entities = new ServiceCollection()
            .UseEntities<ContosoContext>(o => o.UseWriteService(typeof(SwappedWriteService<,,>)));

        var ex = Assert.Throws<InvalidOperationException>(() => entities.For<Course>());
        Assert.That(ex!.Message, Does.Contain(nameof(Course)));
    }
}
