using Regira.Entities.Models;

// ReSharper disable once CheckNamespace
namespace Entities.TestApi.Infrastructure.Persons;

public partial record PersonSearchObject : SearchObject
{
    public ICollection<int>? StudentCourseIds { get; set; }
}