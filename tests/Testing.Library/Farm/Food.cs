namespace Testing.Library.Farm;

public class Food
{
    public int Id { get; set; }
    public string? Name { get; set; }
    public override string ToString() => Name ?? string.Empty;
}