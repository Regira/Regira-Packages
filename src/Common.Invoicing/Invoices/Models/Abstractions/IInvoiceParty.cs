namespace Regira.Invoicing.Invoices.Models.Abstractions;

public interface IInvoiceParty
{
    string Code { get; set; }
    string Title { get; set; }

    ICollection<ContactData>? ContactData { get; set; }
    ICollection<PartyIdentifier>? Identifiers { get; set; }
    /// <summary>
    /// The postal address. Peppol requires one, with a country code, for both the seller and the buyer
    /// </summary>
    PartyAddress? Address => null;
}