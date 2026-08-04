using Regira.Invoicing.Invoices.Abstractions;

namespace Regira.Invoicing.Invoices.Models.Results;

public record SendInvoiceResult : ISendInvoiceResult
{
    public string? InvoiceId { get; set; }
}