using Entities.Testing.Infrastructure.Data;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Models;
using Regira.Entities.Services.Abstractions;
using Regira.Utilities;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Testing;

// Repro for evaluation B3: does the controller Details path (universal IEntityService<TEntity, int>)
// apply a typed [Flags] Includes lambda on a COMPLEX registration?
[TestFixture]
public class TypedDetailsIncludesTests
{
    [Flags]
    public enum CourseTypedIncludes
    {
        Default = 0,
        Department = 1,
        Enrollments = 2,
        All = Department | Enrollments,
    }

    // The single-flag shape agents commonly write: All aliases the one flag.
    [Flags]
    public enum SingleFlagIncludes
    {
        Default = 0,
        Department = 1,
        All = Department,
    }

    public enum CourseTypedSortBy { Default = 0 }
    public record CourseTypedSearchObject : SearchObject;

    private SqliteConnection _connection = null!;
    [SetUp]
    public void Setup()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();
    }
    [TearDown]
    public void TearDown() => _connection.Close();

    [Test]
    public void MaxFlagValue_Is_Or_Of_All_Bits()
    {
        Assert.That((int)(object)EnumUtility.GetMaxFlagValue<CourseTypedIncludes>(), Is.EqualTo(3));
        // All = Department alias must not double-count: max is 1, not 2
        Assert.That((int)(object)EnumUtility.GetMaxFlagValue<SingleFlagIncludes>(), Is.EqualTo(1));
    }

    private async Task<(ServiceProvider sp, int courseId)> BuildTyped<TIncludes>(Func<IQueryable<Course>, TIncludes?, IQueryable<Course>> includes)
        where TIncludes : struct, Enum
    {
        var services = new ServiceCollection();
        services.AddDbContext<ContosoContext>(db => db.UseSqlite(_connection));
        services.UseEntities<ContosoContext>(o => o.UseDefaults())
            .For<Course, int, CourseTypedSearchObject, CourseTypedSortBy, TIncludes>(e =>
            {
                e.Includes(includes);
            });

        var sp = services.BuildServiceProvider();
        var db = sp.GetRequiredService<ContosoContext>();
        await db.Database.EnsureCreatedAsync();
        var course = new Course
        {
            Title = "Typed includes",
            Credits = 3,
            Department = new Department { Title = "Physics" },
        };
        db.Set<Course>().Add(course);
        db.Set<Enrollment>().Add(new Enrollment { Course = course, Student = new Student { GivenName = "A", LastName = "B" } });
        await db.SaveChangesAsync();
        db.ChangeTracker.Clear();
        return (sp, course.Id);
    }

    [Test]
    public async Task Details_Applies_Typed_Gated_Includes_On_Complex_Registration()
    {
        var (sp, id) = await BuildTyped<CourseTypedIncludes>((query, includes) =>
        {
            if (includes?.HasFlag(CourseTypedIncludes.Department) == true)
                query = query.Include(x => x.Department);
            if (includes?.HasFlag(CourseTypedIncludes.Enrollments) == true)
                query = query.Include(x => x.Enrollments);
            return query;
        });
        await using var _ = sp;

        // resolve exactly what the controller Details path resolves
        var service = sp.GetRequiredService<IEntityService<Course, int>>();
        var item = await service.Details(id);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Department, Is.Not.Null, "typed flag-gated reference not loaded on Details");
        Assert.That(item.Enrollments, Is.Not.Null.And.Not.Empty, "typed flag-gated collection not loaded on Details");
    }

    [Test]
    public async Task Details_Applies_Gated_Include_When_All_Aliases_A_Single_Flag()
    {
        var (sp, id) = await BuildTyped<SingleFlagIncludes>((query, includes) =>
        {
            if (includes?.HasFlag(SingleFlagIncludes.Department) == true)
                query = query.Include(x => x.Department);
            return query;
        });
        await using var _ = sp;

        var service = sp.GetRequiredService<IEntityService<Course, int>>();
        var item = await service.Details(id);

        Assert.That(item, Is.Not.Null);
        Assert.That(item!.Department, Is.Not.Null, "alias-valued All corrupted the Details max-includes bitmask");
    }
}
