using Regira.Invoicing.Invoices.Models.Abstractions;

namespace Regira.Invoicing.Invoices.Extensions;

public static class InvoiceLineExtensions
{
    extension(IInvoiceLine line)
    {
        public decimal GetTaxExclusiveAmount()
        {
            return line.UnitPriceExcl * line.Quantity;
        }

        public decimal GetTaxAmount()
        {
            return line.VatPercentage / 100 * line.GetTaxExclusiveAmount();
        }

        public decimal GetTaxInclusiveAmount()
        {
            return line.GetTaxExclusiveAmount() + line.GetTaxAmount();
        }
    }
}