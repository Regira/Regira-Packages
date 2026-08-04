namespace Testing.Library.Farm;

public abstract class Pet(string name) : Animal(name)
{
    public string? Vet { get; set; }

    public ICollection<Human> Humans { get; } = new List<Human>();
}