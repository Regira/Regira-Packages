namespace Regira.Invoicing.Invoices.Models.Web;

public class InvoicePartyInput
{
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public ICollection<ContactDataInput>? ContactData { get; set; } = [];
    public ICollection<PartyIdentifierInput>? Identifiers { get; set; } = [];
}