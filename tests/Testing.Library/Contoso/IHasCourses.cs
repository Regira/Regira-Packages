using Regira.Entities.Models.Abstractions;

namespace Testing.Library.Contoso;

public interface IHasCourses : IHasNormalizedContent
{
    ICollection<Course>? Courses { get; set; }
}
