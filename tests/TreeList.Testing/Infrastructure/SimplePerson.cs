namespace TreeList.Testing.Infrastructure;

public class SimplePerson
{
    public int Id { get; set; }
    public string GivenName { get; set; } = null!;
    public string FamilyName { get; set; } = null!;
    public SimplePerson? Parent { get; set; }
    public ICollection<SimplePerson> Children { get; set; } = null!;

    public override string ToString()
    {
        return $"{GivenName} | {Id} ({Parent?.GivenName ?? ""})";
    }
}