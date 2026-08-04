using Regira.Invoicing.Billit.Models.Identifiers;

namespace Regira.Invoicing.Billit.Models.Contacts;

public class PartyListResponse
{
    public IList<PartyDto> Items { get; set; } = [];
}

public class PartyDto
{
    public long PartyID { get; set; }
    public string Nr { get; set; } = null!;
    public string Name { get; set; } = null!;
    public List<AddressDto>? Addresses { get; set; }
    public string Street { get; set; } = null!;
    public string StreetNumber { get; set; } = null!;
    public string Zipcode { get; set; } = null!;
    public string City { get; set; } = null!;
    public string CountryCode { get; set; } = null!;
    public string IBAN { get; set; } = null!;
    public string BIC { get; set; } = null!;
    public string Contact { get; set; } = null!;
    public string VATNumber { get; set; } = null!;
    public DateTime LastModified { get; set; }
    public DateTime Created { get; set; }
    public string PartyType { get; set; } = null!;
    public bool VATLiable { get; set; }
    public string Language { get; set; } = null!;
    public string VentilationCode { get; set; } = null!;
    public string DisplayName { get; set; } = null!;
    public int DefaultExpiryOffset { get; set; }
    public int GLDefaultExpiryOffset { get; set; }
    public bool VATDeductable { get; set; }
    public List<IdentifierDto> Identifiers { get; set; } = [];
    public bool? SmallEnterprise { get; set; }
    public string? DefaultCurrencyTC { get; set; }
    public bool? IsHighRisk { get; set; }
    public bool SendUBL { get; set; }
    public bool SendPDF { get; set; }
}