using Regira.Invoicing.Billit.Models.Contacts;
using Regira.Invoicing.Billit.Models.Orders;
using Regira.Invoicing.Billit.Models.Orders.Input;
using Regira.Invoicing.Invoices.Models.Abstractions;
using Regira.IO.Extensions;

namespace Regira.Invoicing.Billit.Mapping
{
    public static class MappingExtensions
    {
        public static OrderInput ToOrderInput(this IInvoice item)
        {
            FileInput? pdf = null;
            if (item.Attachments?.Any() == true)
            {
                var file = item.Attachments.First();
                pdf = new FileInput
                {
                    FileContent = file.GetBytes()!,
                    FileName = file.FileName!,
                    MimeType = file.ContentType!
                };
            }

            return new OrderInput
            {
                OrderType = item.InvoiceType == InvoiceType.Invoice ? OrderTypes.Invoice : OrderTypes.CreditNote,
                OrderDirection = OrderDirections.Income,
                OrderNumber = item.Code,
                OrderDate = DateOnly.FromDateTime(item.IssueDate),
                ExpiryDate = DateOnly.FromDateTime(item.DueDate ?? item.IssueDate.AddDays(30)),
                PaymentReference = item.RemittanceInfo,
                OrderPDF = pdf,
                Customer = new CustomerInput
                {
                    Name = item.Customer.Title,
                    VATNumber = item.Customer.Code
                },
                OrderLines = item.InvoiceLines
                    .Select(line => new OrderInput.OrderLineInput
                    {
                        Reference = line.Code,
                        Description = line.Title,
                        DescriptionExtended = line.Description,
                        Quantity = line.Quantity,
                        UnitPriceExcl = line.UnitPriceExcl,
                        VATPercentage = line.VatPercentage,
                    })
                    .ToList(),
                Attachments = item.Attachments?
                    .Skip(1)
                    .Select(file => new FileInput
                    {
                        FileContent = file.GetBytes()!,
                        FileName = file.FileName!,
                        MimeType = file.ContentType!
                    })
                    .ToList()
            };
        }
    }
}
