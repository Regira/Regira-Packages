namespace Regira.Invoicing.Invoices.Models;

public class PartyAddress
{
    public string? Street { get; set; }
    public string? PostalCode { get; set; }
    public string? City { get; set; }
    /// <summary>
    /// ISO 3166-1 alpha-2 code, such as <c>BE</c>
    /// </summary>
    public string CountryCode { get; set; } = null!;
}
