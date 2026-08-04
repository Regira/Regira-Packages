using Regira.Invoicing.Billit.Models.Contacts;
using Regira.Invoicing.Billit.Models.Identifiers;

namespace Regira.Invoicing.Billit.Models.Registrations;

public class RegistrationInput
{
    public string? TaxIdentifier { get; set; }
    public string? CompanyName { get; set; }
    public string? CommercialName { get; set; }
    public bool TaxDeductable { get; set; }
    public bool TaxLiable { get; set; }
    public string? IBAN { get; set; }
    public string? BIC { get; set; }
    public string? Mobile { get; set; }
    public string? Phone { get; set; }
    public string? Email { get; set; }
    public string? ContactFirstName { get; set; }
    public string? ContactLastName { get; set; }
    public string? Language { get; set; }

    public ICollection<IdentifierInput> Identifiers { get; set; } = [];
    public ICollection<AddressInput> Addresses { get; set; } = [];
}