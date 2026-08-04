using Microsoft.EntityFrameworkCore;

namespace Testing.Library.Farm;

public class FarmAnimal(string name, string species) : Animal(name)
{
    public override string Species { get; } = species;

    [Precision(18, 2)]
    public decimal Value { get; set; }

    public override string ToString()
        => $"Farm animal '{Name}' ({Species}/{Id}) worth {Value:C} eats {Food?.ToString() ?? "<Unknown>"}";
}