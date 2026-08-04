namespace Testing.Library.Contoso;

public class Student : Person
{
    public DateTime EnrollmentDate { get; set; }

    public ICollection<Enrollment>? Enrollments { get; set; }
}