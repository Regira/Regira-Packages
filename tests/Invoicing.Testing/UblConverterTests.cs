using System.Xml.Linq;
using Regira.Invoicing.Invoices.Models;
using Regira.Invoicing.UblSharp.Core;
using Regira.Invoicing.UblSharp.Services;

namespace Invoicing.Testing;

/// <summary>
/// The Peppol BIS 3.0 business rules the converter's own output has to meet.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.All)]
public class UblConverterTests
{
    static readonly XNamespace Cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
    static readonly XNamespace Cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";

    static Invoice CreateInvoice(params InvoiceLine[] lines) => new()
    {
        Code = "INV-001",
        IssueDate = new DateTime(2026, 10, 15),
        Supplier = new InvoiceParty { Code = "BE0123456789", Title = "Fleet Services" },
        Customer = new InvoiceParty { Code = "BE0987654321", Title = "Transport Co" },
        InvoiceLines = lines
    };

    static XDocument Convert(Invoice invoice)
        => new UblConverter().Convert(new UblDocumentInput { Invoice = invoice, Supplier = invoice.Supplier });

    static decimal Amount(XElement parent, string name) => decimal.Parse(parent.Element(Cbc + name)!.Value, System.Globalization.CultureInfo.InvariantCulture);

    [Test]
    public void The_Item_Name_Is_The_Line_Title()
    {
        var doc = Convert(CreateInvoice(new InvoiceLine { Title = "Brake service", Quantity = 1, UnitPriceExcl = 100, VatPercentage = 21 }));

        var item = doc.Descendants(Cac + "InvoiceLine").Single().Element(Cac + "Item")!;
        Assert.That(item.Element(Cbc + "Name")!.Value, Is.EqualTo("Brake service"));
    }

    [Test]
    public void Each_Vat_Subtotal_Carries_Its_Tax_Amount()
    {
        var doc = Convert(CreateInvoice(
            new InvoiceLine { Title = "Labour", Quantity = 2, UnitPriceExcl = 50, VatPercentage = 21 },
            new InvoiceLine { Title = "Parts", Quantity = 1, UnitPriceExcl = 40, VatPercentage = 6 }));

        var taxTotal = doc.Root!.Element(Cac + "TaxTotal")!;
        var subtotals = taxTotal.Elements(Cac + "TaxSubtotal").ToList();
        Assert.That(subtotals.Select(s => Amount(s, "TaxAmount")), Is.EquivalentTo(new[] { 21m, 2.40m }));
        // BR-CO-14: the invoice VAT total is the sum of the subtotals
        Assert.That(Amount(taxTotal, "TaxAmount"), Is.EqualTo(23.40m));
    }

    [Test]
    public void Vat_Is_Rounded_Per_Rate_Not_Per_Line()
    {
        // per line 0.066 rounds to 0.07, three times 0.21; per rate 3.30 × 6 % = 0.198 rounds to 0.20 (BR-CO-17)
        var line = () => new InvoiceLine { Title = "Washer fluid", Quantity = 1, UnitPriceExcl = 1.10m, VatPercentage = 6 };
        var doc = Convert(CreateInvoice(line(), line(), line()));

        var taxTotal = doc.Root!.Element(Cac + "TaxTotal")!;
        Assert.That(Amount(taxTotal.Element(Cac + "TaxSubtotal")!, "TaxableAmount"), Is.EqualTo(3.30m));
        Assert.That(Amount(taxTotal.Element(Cac + "TaxSubtotal")!, "TaxAmount"), Is.EqualTo(0.20m));
        Assert.That(Amount(taxTotal, "TaxAmount"), Is.EqualTo(0.20m));
    }

    [Test]
    public void The_Monetary_Totals_Add_Up()
    {
        var line = () => new InvoiceLine { Title = "Washer fluid", Quantity = 1, UnitPriceExcl = 1.10m, VatPercentage = 6 };
        var doc = Convert(CreateInvoice(line(), line(), line(), new InvoiceLine { Title = "Labour", Quantity = 1.5m, UnitPriceExcl = 45.25m, VatPercentage = 21 }));

        var totals = doc.Root!.Element(Cac + "LegalMonetaryTotal")!;
        var vat = Amount(doc.Root!.Element(Cac + "TaxTotal")!, "TaxAmount");
        var lineSum = doc.Descendants(Cac + "InvoiceLine").Sum(l => Amount(l, "LineExtensionAmount"));
        // BR-CO-10, BR-CO-13, BR-CO-15, BR-CO-16
        Assert.That(Amount(totals, "LineExtensionAmount"), Is.EqualTo(lineSum));
        Assert.That(Amount(totals, "TaxExclusiveAmount"), Is.EqualTo(lineSum));
        Assert.That(Amount(totals, "TaxInclusiveAmount"), Is.EqualTo(lineSum + vat));
        Assert.That(Amount(totals, "PayableAmount"), Is.EqualTo(lineSum + vat));
    }

    [Test]
    public void A_Party_Address_Is_Written_As_PostalAddress()
    {
        var invoice = CreateInvoice(new InvoiceLine { Title = "Brake service", Quantity = 1, UnitPriceExcl = 100, VatPercentage = 21 });
        invoice.Supplier.Address = new PartyAddress { Street = "Stationsstraat 1", PostalCode = "9000", City = "Gent", CountryCode = "BE" };

        var doc = Convert(invoice);

        // BR-08, BR-09: the seller's postal address with its country code
        var address = doc.Root!.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!.Element(Cac + "PostalAddress")!;
        Assert.That(address.Element(Cbc + "StreetName")!.Value, Is.EqualTo("Stationsstraat 1"));
        Assert.That(address.Element(Cbc + "CityName")!.Value, Is.EqualTo("Gent"));
        Assert.That(address.Element(Cbc + "PostalZone")!.Value, Is.EqualTo("9000"));
        Assert.That(address.Element(Cac + "Country")!.Element(Cbc + "IdentificationCode")!.Value, Is.EqualTo("BE"));
        // the customer has none
        Assert.That(doc.Root!.Element(Cac + "AccountingCustomerParty")!.Descendants(Cac + "PostalAddress"), Is.Empty);
    }

    // the Peppol rules read an upper-case ISO 3166-1 alpha-2 code (BR-09), and refuse an address without one
    [Test]
    public void A_Country_Code_Is_Written_Upper_Case()
    {
        var invoice = CreateInvoice(new InvoiceLine { Title = "Brake service", Quantity = 1, UnitPriceExcl = 100, VatPercentage = 21 });
        invoice.Supplier.Address = new PartyAddress { City = "Gent", CountryCode = " be " };

        var doc = Convert(invoice);

        var address = doc.Root!.Element(Cac + "AccountingSupplierParty")!.Element(Cac + "Party")!.Element(Cac + "PostalAddress")!;
        Assert.That(address.Element(Cac + "Country")!.Element(Cbc + "IdentificationCode")!.Value, Is.EqualTo("BE"));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void An_Address_Without_A_Country_Code_Throws(string? countryCode)
    {
        var invoice = CreateInvoice(new InvoiceLine { Title = "Brake service", Quantity = 1, UnitPriceExcl = 100, VatPercentage = 21 });
        invoice.Supplier.Address = new PartyAddress { City = "Gent", CountryCode = countryCode! };

        Assert.Throws<ArgumentException>(() => Convert(invoice));
    }

    [Test]
    public void No_Element_Is_Empty()
    {
        // PEPPOL-EN16931-R008: a document must not contain empty elements
        var invoice = CreateInvoice(new InvoiceLine { Title = "Brake service", Quantity = 1, UnitPriceExcl = 100, VatPercentage = 21 });
        invoice.Customer.Address = new PartyAddress { Street = " ", CountryCode = "BE" };
        var doc = Convert(invoice);

        var empty = doc.Descendants().Where(e => !e.HasElements && string.IsNullOrWhiteSpace(e.Value)).Select(e => e.Name.LocalName);
        Assert.That(empty, Is.Empty);
    }

    [TestCase(3, 1.2345)]
    [TestCase(100, 1.2345)]
    [TestCase(7, 0.335)]
    [TestCase(2500, 0.0125)]
    public void A_Line_Amount_Is_Its_Quantity_Times_Its_Price(decimal quantity, decimal unitPrice)
    {
        var doc = Convert(CreateInvoice(new InvoiceLine { Title = "Bolts", Quantity = quantity, UnitPriceExcl = unitPrice, VatPercentage = 21 }));

        var line = doc.Descendants(Cac + "InvoiceLine").Single();
        var price = Amount(line.Element(Cac + "Price")!, "PriceAmount");
        var lineAmount = Amount(line, "LineExtensionAmount");
        // PEPPOL-EN16931-R120: the line amount is quantity × price, within 0.02; the price keeps its decimals
        // (UBL-DT-01 leaves PriceAmount out of its two-decimal limit)
        Assert.That(price, Is.EqualTo(unitPrice));
        Assert.That(Math.Abs(lineAmount - quantity * price), Is.LessThanOrEqualTo(0.02m));
        // BR-DEC-23: the line amount has at most two decimals
        Assert.That(lineAmount, Is.EqualTo(decimal.Round(lineAmount, 2)));
    }

    [Test]
    public void Amounts_Round_Half_Away_From_Zero()
    {
        // 7 × 0.335 = 2.345: half away from zero gives 2.35 (half to even would give 2.34), on the line and in every total
        var doc = Convert(CreateInvoice(new InvoiceLine { Title = "Washers", Quantity = 7, UnitPriceExcl = 0.335m, VatPercentage = 21 }));

        var line = doc.Descendants(Cac + "InvoiceLine").Single();
        var subtotal = doc.Root!.Element(Cac + "TaxTotal")!.Element(Cac + "TaxSubtotal")!;
        var totals = doc.Root!.Element(Cac + "LegalMonetaryTotal")!;
        Assert.That(Amount(line, "LineExtensionAmount"), Is.EqualTo(2.35m));
        Assert.That(Amount(subtotal, "TaxableAmount"), Is.EqualTo(2.35m));
        Assert.That(Amount(totals, "LineExtensionAmount"), Is.EqualTo(2.35m));
        Assert.That(Amount(totals, "TaxExclusiveAmount"), Is.EqualTo(2.35m));
        // 2.35 × 21 % = 0.4935
        Assert.That(Amount(subtotal, "TaxAmount"), Is.EqualTo(0.49m));
        Assert.That(Amount(totals, "PayableAmount"), Is.EqualTo(2.84m));
    }

    [Test]
    public void OutsideScope_Is_Category_O()
    {
        // the converter reads the code from the member ToString() names: either name of the alias must carry it
        var member = typeof(TaxCategoryCode).GetMember(TaxCategoryCode.OutsideScope.ToString()).Single();
        var code = member.GetCustomAttributes(false).Single(a => a.GetType().Name == "PeppolCodeAttribute");
        Assert.That(code.GetType().GetProperty("Value")!.GetValue(code), Is.EqualTo("O"));
#pragma warning disable CS0618 // the misspelt name stays as an alias
        Assert.That(TaxCategoryCode.OutsideScode, Is.EqualTo(TaxCategoryCode.OutsideScope));
#pragma warning restore CS0618
    }
}
