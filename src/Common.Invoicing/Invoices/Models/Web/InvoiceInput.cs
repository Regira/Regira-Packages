using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.Invoices.Models.Web;

public class InvoiceInput
{
    public string Code { get; set; } = null!;
    public string? Title { get; set; }
    public string? Description { get; set; }
    public InvoiceType InvoiceType { get; set; }
    public DateTime IssueDate { get; set; }
    public DateTime? DueDate { get; set; }
    public string? RemittanceInfo { get; set; }
    public InvoicePartyInput Supplier { get; set; } = null!;
    public InvoicePartyInput Customer { get; set; } = null!;
    public ICollection<InvoiceLineInput> InvoiceLines { get; set; } = null!;
    public ICollection<BinaryFileInput>? Attachments { get; set; }
}