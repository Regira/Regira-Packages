using Entities.TestApi.Infrastructure.Courses;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Extensions;
using Regira.Entities.Services.Abstractions;
using Regira.Entities.Web.Controllers;
using Testing.Library.Contoso;
using Testing.Library.Data;

namespace Entities.Web.Testing;

/// <summary>
/// Tests for <see cref="ControllerExtensions.GetRequiredEntityService{TService}"/>.
/// Three error paths:
///   A) Entity IS registered but with different generic params → lists alternatives
///   B) Entity has no registrations at all                    → points to .For&lt;EntityName&gt;()
///   C) IServiceCollection not in container (no UseEntities)  → generic fallback message
/// </summary>
public class GetRequiredEntityServiceTests
{
    private static ControllerBase CreateController(IServiceProvider serviceProvider)
    {
        var httpContext = new DefaultHttpContext { RequestServices = serviceProvider };
        var controller = new EntityServiceTestController();
        controller.ControllerContext = new ControllerContext { HttpContext = httpContext };
        return controller;
    }

    // ── happy path ───────────────────────────────────────────────────────────

    [Fact]
    public void Returns_Service_When_Correctly_Registered()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course, int, CourseSearchObject>();
        using var sp = services.BuildServiceProvider();

        var service = CreateController(sp).GetRequiredEntityService<IEntityService<Course, int, CourseSearchObject>>();
        Assert.NotNull(service);
    }

    // ── path A: entity registered but generic params differ ──────────────────

    [Fact]
    public void PathA_Throws_When_SearchObject_Mismatches()
    {
        // For<Course>() registers IEntityService<Course, int, SearchObject<int>>
        // Requesting CourseSearchObject variant → not registered
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int, CourseSearchObject>>());
    }

    [Fact]
    public void PathA_Message_Contains_Requested_Type_Name()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int, CourseSearchObject>>());

        // Message must name the exact type that could not be resolved
        Assert.Contains("IEntityService", ex.Message);
        Assert.Contains("Course", ex.Message);
        Assert.Contains("CourseSearchObject", ex.Message);
    }

    [Fact]
    public void PathA_Message_Lists_Registered_Alternatives_For_Entity()
    {
        // For<Course>() registers the 1-, 2-, and 3-param IEntityService variants with SearchObject<int>
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int, CourseSearchObject>>());

        // The registered SearchObject<int> variant must appear as an alternative
        Assert.Contains("SearchObject", ex.Message);
    }

    [Fact]
    public void PathA_Message_Tells_User_To_Match_Generic_Parameters_In_For()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int, CourseSearchObject>>());

        Assert.Contains(".For<", ex.Message);
        Assert.Contains("match", ex.Message);
    }

    [Fact]
    public void PathA_Listed_Alternatives_Are_Only_For_The_Requested_Entity()
    {
        // Both Course and Department registered; error for Course must not mention Department
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>().For<Department>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int, CourseSearchObject>>());

        Assert.Contains("Course", ex.Message);
        Assert.DoesNotContain("Department", ex.Message);
    }

    [Fact]
    public void PathA_Inner_Exception_Is_The_Original_DI_Exception()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int, CourseSearchObject>>());

        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // ── path B: entity not registered at all ─────────────────────────────────

    [Fact]
    public void PathB_Throws_When_Entity_Not_Registered()
    {
        // Only Course registered; Department is absent
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Department, int>>());
    }

    [Fact]
    public void PathB_Message_Names_The_Missing_Entity()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Department, int>>());

        Assert.Contains("Department", ex.Message);
    }

    [Fact]
    public void PathB_Message_States_No_Services_Found_For_Entity()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Department, int>>());

        Assert.Contains("no entity services", ex.Message.ToLower());
        Assert.Contains("Department", ex.Message);
    }

    [Fact]
    public void PathB_Message_Suggests_For_Method_With_Concrete_Entity_Name()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Department, int>>());

        // Must suggest the exact call that would fix the problem
        Assert.Contains(".For<Department>()", ex.Message);
    }

    [Fact]
    public void PathB_Message_Mentions_Overload_Option()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Department, int>>());

        Assert.Contains("overload", ex.Message.ToLower());
    }

    [Fact]
    public void PathB_Inner_Exception_Is_The_Original_DI_Exception()
    {
        var services = new ServiceCollection().AddDbContext<ContosoContext>();
        services.UseEntities<ContosoContext>().For<Course>();
        using var sp = services.BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Department, int>>());

        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }

    // ── path C: IServiceCollection not in container (UseEntities not called) ─

    [Fact]
    public void PathC_Throws_When_UseEntities_Not_Called()
    {
        // Bare ServiceCollection — no UseEntities, so IServiceCollection not registered
        using var sp = new ServiceCollection().AddDbContext<ContosoContext>().BuildServiceProvider();

        Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int>>());
    }

    [Fact]
    public void PathC_Message_Points_To_For_Method()
    {
        using var sp = new ServiceCollection().AddDbContext<ContosoContext>().BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int>>());

        Assert.Contains(".For<", ex.Message);
    }

    [Fact]
    public void PathC_Message_Mentions_Matching_Generic_Type_Parameters()
    {
        using var sp = new ServiceCollection().AddDbContext<ContosoContext>().BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int>>());

        Assert.Contains("generic type parameters", ex.Message.ToLower());
    }

    [Fact]
    public void PathC_Inner_Exception_Is_The_Original_DI_Exception()
    {
        using var sp = new ServiceCollection().AddDbContext<ContosoContext>().BuildServiceProvider();

        var ex = Assert.Throws<InvalidOperationException>(() =>
            CreateController(sp).GetRequiredEntityService<IEntityService<Course, int>>());

        Assert.NotNull(ex.InnerException);
        Assert.IsType<InvalidOperationException>(ex.InnerException);
    }
}

internal class EntityServiceTestController : Controller { }
