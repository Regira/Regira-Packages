using Regira.Invoicing.Invoices.Extensions;
using Regira.Invoicing.Invoices.Models;
using Regira.Invoicing.Invoices.Models.Abstractions;
using Regira.Invoicing.UblSharp.Core;
using Regira.Invoicing.UblSharp.Utilities;
using UblSharp.CommonAggregateComponents;
using UblSharp.UnqualifiedDataTypes;

namespace Regira.Invoicing.UblSharp.Extensions;

internal static class InvoiceExtensions
{
    public static PartyType ToPartyType(this IInvoiceParty item)
    {
        var identifier = item.Identifiers?.FirstOrDefault();
        if (identifier == null)
        {
            var identifierSegments = item.Code.Split(':');
            identifier = new PartyIdentifier
            {
                Scheme = identifierSegments.Length > 1 ? identifierSegments.First() : UblDefaults.EndpointSchemeId,
                Value = identifierSegments.Last()
            };
        }
        return new PartyType
        {
            EndpointID = new IdentifierType { schemeID = identifier.Scheme, Value = identifier.Value },
            PartyLegalEntity = new[] { new PartyLegalEntityType {
                RegistrationName = item.Title,
                CompanyID = item.Code
            } }.ToList(),
            PartyTaxScheme = new[] {
                new PartyTaxSchemeType {
                    CompanyID = item.Code,
                    TaxScheme = new TaxSchemeType { ID = UblDefaults.TaxSchemeId }
                }
            }.ToList(),
            PartyIdentification = new[] { new PartyIdentificationType {
                ID = item.Code
            } }.ToList(),
            PartyName = new[] { new PartyNameType { Name = item.Title } }.ToList(),
            Contact = new ContactType
            {
                Name = item.Title,
                Telephone = item.ContactData?.FirstOrDefault(cd => cd.DataType == ContactDataTypes.Phone)?.Value,
                ElectronicMail = item.ContactData?.FirstOrDefault(cd => cd.DataType == ContactDataTypes.Email)?.Value
            }
        };
    }

    //public static FinancialAccountType ToFinancialAccountType(this BankAccount account) => new() { ID = account.AccountNumber };
    public static List<InvoiceLineType> ToInvoiceLineTypeList(this IEnumerable<InvoiceLine> lines) => lines.Select(ToInvoiceLineType).ToList();
    public static InvoiceLineType ToInvoiceLineType(this InvoiceLine line, int index)
    {
        var taxCategoryCode = GetTaxCategoryCode(line);
        var amount = line.Quantity * (line.UnitPriceExcl >= 0 ? 1 : -1);
        return new InvoiceLineType
        {
            ID = (index + 1).ToString(),
            InvoicedQuantity = new QuantityType { unitCode = "NAR", Value = amount },
            InvoicePeriod = line.StartDate.HasValue
                ? [new PeriodType { StartDate = line.StartDate }]
                : null,
            LineExtensionAmount = PeppolUtility.BuildAmountType(amount * Math.Abs(line.UnitPriceExcl)),
            Note = [line.Description],
            //TaxTotal = new List<TaxTotalType> { taxTotalType },
            Item = new ItemType
            {
                Name = new NameType { Value = $"{index + 1}".PadLeft(4, '0') },
                ClassifiedTaxCategory =
                [
                    new TaxCategoryType
                    {
                        ID = PeppolUtility.BuildCodeTypeIdentifierType(taxCategoryCode),
                        Percent = line.VatPercentage,
                        TaxScheme = new TaxSchemeType { ID = "VAT" }
                    }
                ]
            },
            Price = new PriceType
            {
                BaseQuantity = 1,
                PriceAmount = PeppolUtility.BuildAmountType(Math.Abs(line.UnitPriceExcl))
            }
        };
    }
    public static TaxTotalType ToTaxTotalType(this IInvoiceLine line)
    {
        var taxCategoryCode = GetTaxCategoryCode(line);
        return new TaxTotalType
        {
            TaxAmount = PeppolUtility.BuildAmountType(line.Quantity * line.UnitPriceExcl * line.VatPercentage),
            TaxSubtotal = new[] {
                new TaxSubtotalType {
                    TaxableAmount = PeppolUtility.BuildAmountType(line.GetTaxExclusiveAmount()),
                    TaxAmount = PeppolUtility.BuildAmountType(line.GetTaxAmount()),
                    TaxCategory = new TaxCategoryType
                    {
                        ID = PeppolUtility.BuildCodeTypeIdentifierType(taxCategoryCode),
                        Percent = line.VatPercentage,
                        TaxScheme = new TaxSchemeType { ID = "VAT" },
                        //TaxExemptionReason = new List<TextType> { line.TaxCategory.Statement}
                    }
                }
            }.ToList()
        };
    }
    public static List<TaxTotalType> ToTaxTotalTypeList(this IEnumerable<InvoiceLine> lines)
    {
        var linesList = lines as InvoiceLine[] ?? lines.ToArray();
        var taxTotal = new List<TaxTotalType>
        {
            new TaxTotalType
            {
                TaxAmount = PeppolUtility.BuildAmountType(linesList.Sum(line => decimal.Round(line.GetTaxAmount(), 2))),
                TaxSubtotal = linesList
                    .GroupBy(line => line.VatPercentage)
                    .Select(linePerBtw => ToTaxTotalType(linePerBtw, GetTaxCategoryCode(linePerBtw.First())))
                    .ToList()
            }
        };

        return taxTotal;
    }
    public static TaxSubtotalType ToTaxTotalType(this IEnumerable<IInvoiceLine> lines, TaxCategoryCode taxCategoryCode)
    {
        var linesList = lines as InvoiceLine[] ?? lines.ToArray();
        var firstLine = linesList.First();
        var taxableAmount = PeppolUtility.BuildAmountType(linesList.Sum(line => decimal.Round(line.GetTaxExclusiveAmount(), 2)));
        //var taxAmount = PeppolUtility.BuildAmountType(linesList.Sum(line => decimal.Round(line.TaxTariff ?? -1, 2)));
        var taxCategoryId = PeppolUtility.BuildCodeTypeIdentifierType(taxCategoryCode);
        return new TaxSubtotalType
        {
            TaxableAmount = taxableAmount,
            //TaxAmount = taxAmount,
            TaxCategory = new TaxCategoryType
            {
                ID = taxCategoryId,
                Percent = firstLine.VatPercentage,
                //Percent = firstLine.VatPercentage .TaxCategory!.Tariff,
                TaxScheme = new TaxSchemeType { ID = "VAT" },
                //TaxExemptionReason = new List<TextType> { firstLine.TaxCategory.Statement }
            }
        };
    }

    public static TaxCategoryCode GetTaxCategoryCode(IInvoiceLine? taxCategory)
    {
        //if (taxCategory != null)
        //{
        //    // Expect a code as string, exclude integers
        //    if (!int.TryParse(taxCategory.Code, out _) &&
        //        Enum.TryParse<TaxCategoryCode>(taxCategory.Code, true, out var code))
        //    {
        //        return code;
        //    }
        //}

        return TaxCategoryCode.Standard;
    }
}