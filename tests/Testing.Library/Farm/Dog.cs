namespace Testing.Library.Farm;

public class Dog(string name, string favoriteToy) : Pet(name)
{
    public string FavoriteToy { get; set; } = favoriteToy;
    public override string Species => "Canis familiaris";

    public override string ToString()
        => $"Dog '{Name}' ({Species}/{Id}) with favorite toy '{FavoriteToy}' eats {Food?.ToString() ?? "<Unknown>"}";
}