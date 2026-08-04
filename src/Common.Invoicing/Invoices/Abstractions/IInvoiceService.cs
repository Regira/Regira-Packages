using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.Invoices.Abstractions;

public interface IInvoiceService
{
    Task<ICreateInvoiceResult> Create(IInvoice item);
    Task<ISendInvoiceResult> Send(IInvoice input);
}