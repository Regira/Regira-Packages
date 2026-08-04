namespace TreeList.Testing.Infrastructure;

public class Person
{
    public int Id { get; set; }
    public string GivenName { get; set; } = null!;
    public string FamilyName { get; set; } = null!;

    public ICollection<Relation> Contacts { get; set; } = new List<Relation>();


    public override string ToString()
    {
        return $"{GivenName} | {Id} ({string.Join(",", Contacts.Select(c => c.Contact.Id))}";
    }
}