using Regira.Invoicing.Billit.Models.Identifiers;

namespace Regira.Invoicing.Billit.Models.Contacts;

public class CustomerInput
{
    public string Name { get; set; } = null!;
    public string VATNumber { get; set; } = null!;
    public string PartyType { get; set; } = null!;
    public string? Email { get; set; }
    public List<IdentifierInput>? Identifiers { get; set; }
    public List<AddressInput>? Addresses { get; set; }
}