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
            PostalAddress = item.Address?.ToAddressType(),
            Contact = new ContactType
            {
                Name = item.Title,
                Telephone = item.ContactData?.FirstOrDefault(cd => cd.DataType == ContactDataTypes.Phone)?.Value,
                ElectronicMail = item.ContactData?.FirstOrDefault(cd => cd.DataType == ContactDataTypes.Email)?.Value
            }
        };
    }

    public static AddressType ToAddressType(this PartyAddress address) => new()
    {
        StreetName = NullIfBlank(address.Street),
        PostalZone = NullIfBlank(address.PostalCode),
        CityName = NullIfBlank(address.City),
        Country = new CountryType { IdentificationCode = address.CountryCode }
    };
    static string? NullIfBlank(string? value) => string.IsNullOrWhiteSpace(value) ? null : value;

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
                Name = new NameType { Value = NullIfBlank(line.Title) ?? $"{index + 1}".PadLeft(4, '0') },
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
                PriceAmount = PeppolUtility.BuildPriceAmountType(Math.Abs(line.UnitPriceExcl))
            }
        };
    }
    public static List<TaxTotalType> ToTaxTotalTypeList(this IEnumerable<InvoiceLine> lines)
    {
        var subtotals = lines
            .GroupBy(line => line.VatPercentage)
            .Select(linesPerRate => ToTaxSubtotalType(linesPerRate.ToArray(), GetTaxCategoryCode(linesPerRate.First())))
            .ToList();
        return
        [
            new TaxTotalType
            {
                // BR-CO-14: the sum of the subtotals
                TaxAmount = PeppolUtility.BuildAmountType(subtotals.Sum(subtotal => subtotal.TaxAmount.Value)),
                TaxSubtotal = subtotals
            }
        ];
    }
    public static TaxSubtotalType ToTaxSubtotalType(this IInvoiceLine[] lines, TaxCategoryCode taxCategoryCode)
    {
        var firstLine = lines.First();
        // the same rounded line amounts as LineExtensionAmount
        var taxableAmount = lines.Sum(line => PeppolUtility.RoundAmount(line.GetTaxExclusiveAmount()));
        var taxCategoryId = PeppolUtility.BuildCodeTypeIdentifierType(taxCategoryCode);
        return new TaxSubtotalType
        {
            TaxableAmount = PeppolUtility.BuildAmountType(taxableAmount),
            // BR-CO-17: VAT is rounded once per rate, not per line
            TaxAmount = PeppolUtility.BuildAmountType(taxableAmount * firstLine.VatPercentage / 100),
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