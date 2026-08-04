namespace Testing.Library.Farm;

public class Cat(string name, string educationLevel) : Pet(name)
{
    public string EducationLevel { get; set; } = educationLevel;
    public override string Species => "Felis catus";

    public override string ToString()
        => $"Cat '{Name}' ({Species}/{Id}) with education '{EducationLevel}' eats {Food?.ToString() ?? "<Unknown>"}";
}