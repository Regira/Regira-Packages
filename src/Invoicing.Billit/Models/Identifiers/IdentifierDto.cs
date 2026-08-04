namespace Regira.Invoicing.Billit.Models.Identifiers;

public class IdentifierDto
{
    public string IdentifierType { get; set; } = null!;
    public string Identifier { get; set; } = null!;
    public string? SchemeID { get; set; }
    public bool Preferred { get; set; }
}