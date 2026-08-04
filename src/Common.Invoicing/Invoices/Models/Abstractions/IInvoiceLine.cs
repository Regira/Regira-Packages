namespace Regira.Invoicing.Invoices.Models.Abstractions;

public interface IInvoiceLine
{
    string? Code { get; set; }
    string Title { get; set; }
    string? Description { get; set; }
    decimal Quantity { get; set; }
    decimal UnitPriceExcl { get; set; }
    decimal VatPercentage { get; set; }

    public DateTime? StartDate { get; set; }
    public DateTime? EndDate { get; set; }
}