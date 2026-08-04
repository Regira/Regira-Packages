using Regira.Invoicing.Billit.Models.Identifiers;

namespace Regira.Invoicing.Billit.Models.Contacts;

public class CounterPartyDto
{
    public int PartyId { get; set; }
    public List<AddressDto>? Addresses { get; set; }
    public string? Street { get; set; }
    public string? StreetNumber { get; set; }
    public string? Box { get; set; }
    public string? City { get; set; }
    public string? CountryCode { get; set; }
    public string? VatNumber { get; set; }
    public string? PartyType { get; set; }
    public bool VatLiable { get; set; }
    public string? DisplayName { get; set; }
    public bool VatDeductable { get; set; }
    public List<IdentifierDto> Identifiers { get; set; } = null!;
    public bool SendUbl { get; set; }
    public bool SendPdf { get; set; }
}