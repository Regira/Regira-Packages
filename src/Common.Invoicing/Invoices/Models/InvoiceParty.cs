using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.Invoices.Models;

public class InvoiceParty : IInvoiceParty
{
    public string Code { get; set; } = null!;
    public string Title { get; set; } = null!;
    public ICollection<ContactData>? ContactData { get; set; } = [];
    public ICollection<PartyIdentifier>? Identifiers { get; set; } = [];
}