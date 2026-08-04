namespace Regira.Invoicing.Invoices.Abstractions;

public interface ISendInvoiceResult
{
    string? InvoiceId { get; set; }
}