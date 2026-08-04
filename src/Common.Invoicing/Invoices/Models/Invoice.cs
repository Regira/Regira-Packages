using Regira.Invoicing.Invoices.Models.Abstractions;
using Regira.IO.Models;

namespace Regira.Invoicing.Invoices.Models;

public class Invoice : IInvoice
{
    public string Code { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? RemittanceInfo { get; set; }
    public InvoiceParty Supplier { get; set; } = new();
    public InvoiceParty Customer { get; set; } = new();
    public ICollection<InvoiceLine> InvoiceLines { get; set; } = [];
    public ICollection<BinaryFileItem>? Attachments { get; set; }
}