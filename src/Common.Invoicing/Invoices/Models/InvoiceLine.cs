using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.Invoices.Models;

public class InvoiceLine : IInvoiceLine
{
    public string? Code { get; set; }
    public string Title { get; set; } = null!;
    public string? Description { get; set; }
    public decimal Quantity { get; set; }
    public decimal UnitPriceExcl { get; set; }
    public decimal VatPercentage { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}