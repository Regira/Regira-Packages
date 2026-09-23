using Entities.TestApi.Controllers;
using Entities.TestApi.Infrastructure.Courses;
using Entities.TestApi.Infrastructure.Persons;
using Testing.Library.Contoso;
using Microsoft.Extensions.DependencyInjection;
using Regira.Entities.DependencyInjection.Mapping;
using Regira.Entities.DependencyInjection.Validation;
using Regira.Entities.Web.Validation;

namespace Entities.Web.Testing;

// Declaring the DTOs on the controller alone is the documented default, and the DTO checks of startup validation
// (concurrency token, attachments collection) saw only UseMapping registrations — so without one they never looked.
// The web package hands them the shapes the controllers bind.
public class ControllerDtoShapeSourceTests
{
    [Fact]
    public void The_Entity_Controllers_Generic_Dtos_Reach_The_Dto_Checks()
    {
        var services = new ServiceCollection();
        services.AddControllers().AddApplicationPart(typeof(CourseController).Assembly);
        services.ValidateEntityControllers();
        using var sp = services.BuildServiceProvider();

        var shapes = sp.GetServices<IEntityDtoShapeSource>().SelectMany(s => s.GetDtoShapes()).ToList();

        Assert.Contains(new EntityMappingRegistration(typeof(Course), typeof(CourseDto), typeof(CourseInputDto)), shapes);
        Assert.Contains(new EntityMappingRegistration(typeof(Person), typeof(PersonDto), typeof(PersonInputDto)), shapes);
    }
}
