namespace TreeList.Testing.Infrastructure;

public class Relation
{
    public Person Person { get; set; } = null!;
    public Person Contact { get; set; } = null!;
    public string RelationName { get; set; } = null!;


    public override string ToString()
    {
        return $"{Person.GivenName} ~ {Contact.GivenName}";
    }
}