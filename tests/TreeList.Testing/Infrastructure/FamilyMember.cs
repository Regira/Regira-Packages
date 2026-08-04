namespace TreeList.Testing.Infrastructure;

public class FamilyMember
{
    public int Id { get; set; }
    public string Name { get; set; } = null!;
    public ICollection<FamilyMember>? Parents { get; set; } = [];

    public override string ToString() => Name;
}
