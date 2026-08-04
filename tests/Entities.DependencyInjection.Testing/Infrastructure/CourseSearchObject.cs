using Regira.Entities.Models;

namespace Entities.DependencyInjection.Testing.Infrastructure;

public record CourseSearchObject : SearchObject
{
    public int? DepartmentId { get; set; }
}

