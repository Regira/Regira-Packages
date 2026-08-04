using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.UblSharp.Core;

public class UblDocumentInput
{
    public IInvoice Invoice { get; set; } = null!;
    public IInvoiceParty? Supplier { get; set; }
    public string? PaymentConditions { get; set; }
}