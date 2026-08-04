namespace Testing.Library.Farm;
public abstract class Animal(string name)
{
    public int Id { get; set; }
    public string Name { get; set; } = name;
    public abstract string Species { get; }

    public Food? Food { get; set; }
}