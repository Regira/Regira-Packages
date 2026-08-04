namespace Regira.Office.VCards.Serializing;

public class VCardPropertyAttribute(string name) : Attribute
{
    public string Name { get; set; } = name;
}