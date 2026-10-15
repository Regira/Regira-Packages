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
}
