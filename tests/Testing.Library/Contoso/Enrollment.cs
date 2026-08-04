using Regira.Entities.Models.Abstractions;

namespace Testing.Library.Contoso;

public class Enrollment : IEntityWithSerial
{
    public int Id { get; set; }
    public int CourseId { get; set; }
    public int StudentId { get; set; }
    public Grade? Grade { get; set; }

    public Course? Course { get; set; }
    public Student? Student { get; set; }
}