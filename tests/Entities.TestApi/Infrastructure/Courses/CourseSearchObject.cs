using Regira.Entities.Models;

namespace Entities.TestApi.Infrastructure.Courses;

public record CourseSearchObject : SearchObject
{
    public int? DepartmentId { get; set; }
}

