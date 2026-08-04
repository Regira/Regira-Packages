namespace Testing.Library.Farm;

public class Human(string name) : Animal(name)
{
    public override string Species => "Homo sapiens";

    public Animal? FavoriteAnimal { get; set; }
    public ICollection<Pet> Pets { get; } = new List<Pet>();

    public override string ToString()
        => $"Human '{Name}' ({Species}/{Id}) with favorite animal '{FavoriteAnimal?.Name ?? "<Unknown>"}'" +
           $" eats {Food?.ToString() ?? "<Unknown>"}";
}