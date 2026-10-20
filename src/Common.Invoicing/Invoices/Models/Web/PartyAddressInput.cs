namespace Regira.Invoicing.Invoices.Models.Web;

public class PartyAddressInput
{
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    public string CountryCode { get; set; } = null!;
}
