using Testing.Library.Contoso;

namespace Testing.Library.Data;

public class People(params Person[] items)
{
    public Student John { get; set; } = items.OfType<Student>().Single(p => p.GivenName == nameof(John));
    public Student Jane { get; set; } = items.OfType<Student>().Single(p => p.GivenName == nameof(Jane));
    public Student Francois { get; set; } = items.OfType<Student>().Single(p => p.GivenName == "François");
    public Instructor Bob { get; set; } = items.OfType<Instructor>().Single(p => p.GivenName == "Robert");
    public Instructor Bill { get; set; } = items.OfType<Instructor>().Single(p => p.GivenName == "Richard");
    public Instructor David { get; set; } = items.OfType<Instructor>().Single(p => p.GivenName == nameof(David));

    public IList<Person> All => [John, Jane, Francois, Bob, Bill];
}