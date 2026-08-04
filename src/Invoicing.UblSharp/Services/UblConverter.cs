using Regira.Invoicing.Invoices.Extensions;
using Regira.Invoicing.Invoices.Models;
using Regira.Invoicing.UblSharp.Abstractions;
using Regira.Invoicing.UblSharp.Core;
using Regira.Invoicing.UblSharp.Extensions;
using Regira.Invoicing.UblSharp.Utilities;
using System.Xml.Linq;
using UblSharp;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;
using UblInvoiceType = UblSharp.InvoiceType;

namespace Regira.Invoicing.UblSharp.Services;

public class UblConverter : IUblConverter
{
    public XDocument Convert(UblDocumentInput input)
    {
        var doc = CreateDocument(input);

        using var ms = new MemoryStream();
        UblDocument.Save(doc, ms);
        ms.Position = 0;
        var xdoc = XDocument.Load(ms);

        return xdoc;
    }

    protected internal UblInvoiceType CreateDocument(UblDocumentInput input)
    {
        var invoice = input.Invoice;

        var invoiceLines = invoice.InvoiceLines?.ToInvoiceLineTypeList();
        var taxTotalLines = invoice.InvoiceLines?.ToTaxTotalTypeList();
        var invoiceLineDates = invoice.InvoiceLines?
            .Where(line => line.StartDate.HasValue)
            .Select(line => line.StartDate!.Value)
            .ToList();

        var invoiceTypeCode = PeppolUtility.GetPeppolCodeValue(InvoiceTypeCode.Commercial);
        var invoicePeriod = (invoiceLineDates?.Any() ?? false)
            ? new List<PeriodType> {
                new()
                {
                    StartDate = invoiceLineDates.Min(),
                    EndDate = invoiceLineDates.Max()
                }
            }
            : null;
        var supplier = new SupplierPartyType
        {
            Party = input.Supplier?.ToPartyType()
        };
        var customer = new CustomerPartyType
        {
            Party = invoice.Customer?.ToPartyType()
        };
        var paymentMeans = new[] {
            new PaymentMeansType {
                PaymentMeansCode = PeppolUtility.GetPeppolCodeValue(PaymentMeansCode.NotDefined),
                PaymentID = !string.IsNullOrEmpty(invoice.RemittanceInfo)
                    ? new IdentifierType[] {invoice.RemittanceInfo}.ToList()
                    : null,
                //PayeeFinancialAccount = input.Supplier?.BankAccounts
                //    ?.FirstOrDefault(a=>a.AccountType== BankAccountType.IBAN.ToString())
                //    ?.ToFinancialAccountType()
            }
        }.ToList();
        var paymentTerms = !string.IsNullOrEmpty(input.PaymentConditions)
            ? new[] {
                new PaymentTermsType {
                    Note = new TextType[] {input.PaymentConditions}.ToList()
                }
            }.ToList()
            : null;
        var monetaryTotal = new MonetaryTotalType
        {
            LineExtensionAmount = PeppolUtility.BuildAmountType(GetAmount(invoice.InvoiceLines, x => x.UnitPriceExcl * x.Quantity)),
            TaxExclusiveAmount = PeppolUtility.BuildAmountType(GetAmount(invoice.InvoiceLines, x => x.GetTaxExclusiveAmount())),
            TaxInclusiveAmount = PeppolUtility.BuildAmountType(GetAmount(invoice.InvoiceLines, x => x.GetTaxInclusiveAmount())),
            PayableAmount = PeppolUtility.BuildAmountType(GetAmount(invoice.InvoiceLines, x => x.GetTaxInclusiveAmount()))
        };

        var attachments = invoice.Attachments?
            .Select(file =>
                new DocumentReferenceType
                {
                    ID = invoice.Code,
                    Attachment = new AttachmentType
                    {
                        EmbeddedDocumentBinaryObject = new BinaryObjectType
                        {
                            Value = file.Bytes,
                            filename = file.FileName,
                            mimeCode = file.ContentType
                        }
                    }
                })
            .ToList();
        var doc = new UblInvoiceType
        {
            CustomizationID = UblConstants.CUSTOMIZATION_ID,
            ProfileID = UblConstants.PROFILE_ID,
            ID = invoice.Code,
            IssueDate = invoice.IssueDate,
            DueDate = invoice.DueDate,
            InvoiceTypeCode = invoiceTypeCode,
            InvoicePeriod = invoicePeriod,
            DocumentCurrencyCode = "EUR",
            OrderReference = new OrderReferenceType { ID = invoice.Code },
            AccountingSupplierParty = supplier,
            AccountingCustomerParty = customer,
            PaymentMeans = paymentMeans,
            PaymentTerms = paymentTerms,
            Note = new TextType[] { invoice.Description }.ToList(),
            TaxTotal = taxTotalLines,
            LegalMonetaryTotal = monetaryTotal,
            InvoiceLine = invoiceLines,
            AdditionalDocumentReference = attachments
        };
        return doc;
    }
    protected internal decimal GetAmount(ICollection<InvoiceLine>? lines, Func<InvoiceLine, decimal> selector)
    {
        return lines?.Sum(x => decimal.Round(selector(x), 2)) ?? 0;
    }
}