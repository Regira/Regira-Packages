using Regira.Invoicing.Invoices.Abstractions;

namespace Regira.Invoicing.Invoices.Models.Results;

public record CreateInvoiceResult : ICreateInvoiceResult
{
    public string InvoiceId { get; set; } = null!;
}