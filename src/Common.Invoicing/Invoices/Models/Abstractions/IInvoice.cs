using Regira.IO.Models;

namespace Regira.Invoicing.Invoices.Models.Abstractions;

public interface IInvoice
{
    string Code { get; set; }
    string? Title { get; set; }
    string? Description { get; set; }
    InvoiceType InvoiceType { get; set; }
    DateTime IssueDate { get; set; }
    DateTime? DueDate { get; set; }
    string? RemittanceInfo { get; set; }
    InvoiceParty Supplier { get; set; }
    InvoiceParty Customer { get; set; }
    ICollection<InvoiceLine> InvoiceLines { get; set; }
    ICollection<BinaryFileItem>? Attachments { get; set; }
}