namespace Common.Testing.Models;

public enum PersonType
{
    Unknown,
    Parent,
    Brother,
    Spouse
}
public class Person
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public PersonType? PersonType { get; set; }
    public DateTime? BirthDate { get; set; }
    public int? Weight { get; set; }
    public Person? Spouse { get; set; }
    public ICollection<string>? Hobbies { get; set; }
    public IDictionary<string, object>? MetaData { get; set; }
}