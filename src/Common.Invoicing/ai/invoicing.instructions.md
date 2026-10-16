# Regira Invoicing AI Agent Instructions

> Electronic invoice creation, UBL/Peppol conversion, and document transmission via an AP gateway.

## Projects

| Project | Package | Purpose |
|---|---|---|
| `Common.Invoicing` | `Regira.Invoicing` | Shared abstractions |
| `Invoicing.Billit` | `Regira.Invoicing.Billit` | Create and send invoices via Billit |
| `Invoicing.UblSharp` | `Regira.Invoicing.UblSharp` | Convert invoices to UBL XML (Peppol BIS) |
| `Invoicing.ViaAdValvas` | `Regira.Invoicing.ViaAdValvas` | Transmit UBL documents via AdValVas AP gateway |

---

## Installation

```xml
<!-- Billit — create and send invoices via Billit -->
<PackageReference Include="Regira.Invoicing.Billit" Version="6.*" />

<!-- UblSharp — convert invoices to UBL XML (Peppol BIS) -->
<PackageReference Include="Regira.Invoicing.UblSharp" Version="6.*" />

<!-- ViaAdValvas — transmit UBL documents via AdValVas AP gateway -->
<PackageReference Include="Regira.Invoicing.ViaAdValvas" Version="6.*" />

<!-- the ISerializer Billit's managers and PeppolService take -->
<PackageReference Include="Regira.Serializing.Newtonsoft" Version="6.*" />
```

---

## Billit

### `BillitConfig`

| Property | Type | Description |
|---|---|---|
| `PartyId` | `string?` | Your Billit party ID |
| `Api.BaseUrl` | `string?` | The Billit API root (production or sandbox, from Billit). Required: there is no default, and a missing one throws `NullReferenceException` when the first manager is created |
| `Api.Key` | `string?` | API key |

### DI Registration

```csharp
using Regira.Invoicing.Billit.Config;                // BillitConfig
using Regira.Invoicing.Billit.DependencyInjection;   // AddBillit

services.AddBillit(sp => new BillitConfig
{
    PartyId = configuration["Billit:PartyId"],
    Api     = new() { BaseUrl = configuration["Billit:Api:Url"], Key = configuration["Billit:Api:Key"] }
});
// Registers: IInvoiceManager, IFileManager, IPartyManager, IPeppolManager (transient).
// IInvoiceManager extends IInvoiceService, which AddBillit registers too (an IInvoiceService of your own wins).
// The managers also need an ISerializer, which AddBillit does not register:
services.AddSingleton<Regira.Serializing.Abstractions.ISerializer, Regira.Serializing.Newtonsoft.Json.JsonSerializer>();
```

### `IInvoiceManager`

<!-- no-compile -->
```csharp
Task<ICreateInvoiceResult> Create(IInvoice item);
Task<ISendInvoiceResult>   Send(params string[] ids);   // send by IDs
Task<ISendInvoiceResult>   Send(IInvoice input);         // creates the invoice, then sends it
```

---

## UblSharp — UBL Conversion

### `IUblConverter`

<!-- no-compile -->
```csharp
XDocument Convert(UblDocumentInput input);
```

Produces a UBL 2.1 `Invoice` document. `IUblDocumentValidator` and `IUblDocumentManager` are `[Obsolete]`: no package
implements them, so validate with a Peppol validator of your own choice.

```csharp
IInvoice invoice = new Invoice
{
    Code = "INV-001",
    IssueDate = DateTime.Today,
    Supplier = new InvoiceParty
    {
        Code = "BE0123456789",                                                         // VAT number
        Title = "Fleet Services",
        Identifiers = [new PartyIdentifier { Scheme = "0208", Value = "0123456789" }],   // Peppol participant ID
        Address = new PartyAddress { Street = "Stationsstraat 1", PostalCode = "9000", City = "Gent", CountryCode = "BE" }
    },
    Customer = new InvoiceParty
    {
        Code = "BE0987654321",
        Title = "Transport Co",
        Identifiers = [new PartyIdentifier { Scheme = "0208", Value = "0987654321" }],
        Address = new PartyAddress { Street = "Kaai 12", PostalCode = "2000", City = "Antwerpen", CountryCode = "BE" }
    },
    InvoiceLines = [new InvoiceLine { Title = "Brake service", Quantity = 1, UnitPriceExcl = 100, VatPercentage = 21 }]
};

var converter = new UblConverter();
XDocument ubl = converter.Convert(new UblDocumentInput
{
    Invoice  = invoice,            // required
    Supplier = invoice.Supplier    // the seller: read from here only, never from Invoice
});
```

`UblDocumentInput` has three properties:

| Property | Type | Description |
|---|---|---|
| `Invoice` | `IInvoice` | Required. Code, dates, customer, lines, remittance info and attachments |
| `Supplier` | `IInvoiceParty?` | The seller, written as `AccountingSupplierParty`. The converter does not read `Invoice.Supplier`, so a document converted without it has no seller, which Peppol validation refuses (EN 16931 rule BR-06) |
| `PaymentConditions` | `string?` | Written as the payment terms note |

### How the model maps to UBL

| Member | UBL element |
|---|---|
| `IInvoiceParty.Code` | `PartyTaxScheme/CompanyID`, `PartyLegalEntity/CompanyID` and `PartyIdentification/ID`: give the VAT number with its country prefix (`BE0123456789`, rule BR-CO-09) |
| `IInvoiceParty.Identifiers` | The first entry is the `EndpointID`, the Peppol participant ID, with `Scheme` as its `schemeID` (`0208` for a Belgian enterprise number). Without one, `Code` is split on `:`, and a code without `:` gets scheme `9956` (`UblDefaults.EndpointSchemeId`), the legacy Belgian enterprise-number scheme that `0208` replaces; a Belgian VAT number is an endpoint only under scheme `9925` (`9925:BE0123456749`). Always give the identifier |
| `IInvoiceParty.Title` | `PartyName/Name`, `PartyLegalEntity/RegistrationName` and `Contact/Name` |
| `IInvoiceParty.Address` | `PostalAddress`. Peppol requires one with a `CountryCode` for both parties (BR-08 to BR-11); `IInvoiceParty` implementations without the property write none |
| `IInvoiceParty.ContactData` | `Contact/Telephone` and `Contact/ElectronicMail`: the first phone and the first e-mail entry |
| `IInvoice.Code` | `ID`, `OrderReference/ID` and each attachment's `ID`. `BuyerReference` (BT-10) is not written: when the buyer gave one, add it to the returned `XDocument` |
| `IInvoice.IssueDate`, `DueDate` | `IssueDate`, `DueDate` |
| `IInvoice.Description` | The invoice `Note` |
| `IInvoice.Attachments` | `AdditionalDocumentReference`, each file embedded |
| `IInvoiceLine.Title` | `Item/Name` |
| `IInvoiceLine.Description` | The line `Note` |
| `IInvoiceLine.Quantity`, `UnitPriceExcl` | `InvoicedQuantity` (unit `NAR`); `Price/PriceAmount` with the price's own decimals; `LineExtensionAmount`, quantity × price rounded to two decimals, half away from zero like every amount in the document. A negative price is written as a negative quantity |
| `IInvoiceLine.VatPercentage` | `ClassifiedTaxCategory/Percent`, and one `TaxSubtotal` per rate |
| `IInvoiceLine.StartDate` | The line's `InvoicePeriod/StartDate`. The earliest and latest start dates form the invoice's `InvoicePeriod` |

VAT is computed per rate, not per line: a rate's taxable amount is the sum of its rounded line amounts, and its VAT is
that amount times the rate, rounded once, half away from zero (BR-CO-17). The invoice's VAT total, `TaxInclusiveAmount`
and `PayableAmount` follow from those subtotals. Compute a printed invoice's totals the same way, or they can differ
from the UBL document by a cent.

### What the converter writes

Some fields are fixed rather than taken from the invoice:

- Customization ID and Profile ID from `UblConstants`: Peppol BIS Billing 3.0
- Invoice type code `380`, a commercial invoice, for every document. `IInvoice.InvoiceType` is not read, so the
  converter writes no credit notes
- Currency `EUR`
- Payment means `1` (not defined), with `RemittanceInfo` as the payment ID
- Tax category `S` (standard rate) on every line. Peppol refuses `S` with a 0 % rate (BR-S-05), so an exempt,
  zero-rated or reverse-charge line needs its category changed in the returned `XDocument`

For exempt 0 % lines, post-process the document: set each 0 % category to `E`, and give each VAT breakdown's
`TaxCategory` an exemption reason (BR-E-10), right after `Percent` as the schema orders it. A line's
`ClassifiedTaxCategory` takes no reason:

```csharp
using System.Xml.Linq;

public static class UblExemptions
{
    // ubl = converter.Convert(input).MarkExemptCategories("Exempt under article 44 of the VAT Code");
    public static XDocument MarkExemptCategories(this XDocument ubl, string exemptionReason)
    {
        XNamespace cac = Regira.Invoicing.Peppol.PeppolNamespaces.CAC;
        XNamespace cbc = Regira.Invoicing.Peppol.PeppolNamespaces.CBC;
        var zeroRated = ubl.Descendants(cac + "TaxCategory")             // TaxTotal/TaxSubtotal
            .Concat(ubl.Descendants(cac + "ClassifiedTaxCategory"))     // InvoiceLine/Item
            .Where(category => (decimal?)category.Element(cbc + "Percent") == 0)
            .ToList();
        foreach (var category in zeroRated)
        {
            category.Element(cbc + "ID")!.Value = "E";
            if (category.Name == cac + "TaxCategory")
            {
                category.Element(cbc + "Percent")!.AddAfterSelf(new XElement(cbc + "TaxExemptionReason", exemptionReason));
            }
        }
        return ubl;
    }
}
```

Not written: `IInvoiceLine.Code`, `IInvoiceLine.EndDate` and a payee bank account.

---

## ViaAdValvas — Peppol Transmission

### `GatewaySettings`

| Property | Type | Description |
|---|---|---|
| `Uri` | `string` | AdValVas gateway endpoint |
| `SenderID` | `string` | Your Peppol participant ID |
| `SenderName` | `string` | Display name |
| `Token` | `string` | API token |
| `SecretKey` | `string` | Secret key included in the request seal |
| `IsProduction` | `bool` | Not read: `PeppolService` posts to `Uri`, so point `Uri` at the test or the production gateway |

### `PeppolService`

<!-- no-compile -->
```csharp
var service = new PeppolService(gatewaySettings, new Regira.Serializing.Newtonsoft.Json.JsonSerializer());   // the ISerializer it takes

try
{
    // returns only once the gateway accepted the document, so result.Success is always true here
    UblDocumentResponse result = await service.Send(ublDocument);
    Console.WriteLine($"Sent. Reference: {result.Reference}");
}
catch (PeppolRequestException ex)    // the gateway answered with a non-2xx status
{
    Console.WriteLine($"Rejected ({ex.ServiceStatusCode}): {ex.ResponseContent}");
}
catch (PeppolResponseException ex)   // it answered, but did not accept the document
{
    Console.WriteLine($"Not sent: {ex.SerializedResponse}");
}
```

Every instance sends through a connection pool the package shares, so it can be registered with any lifetime. Never repeat a `Send` that may have reached the gateway: the document would go out twice.

`PeppolRequestException` and `PeppolResponseException` are in `Regira.Invoicing.ViaAdValvas.Models`.

Requests are sealed with `SealUtility.Generate()`: an MD5 digest over the token, sender ID, reference ID, date and
the secret key, a plain hash with the secret appended, not an HMAC.

---

## Typical End-to-End Flow

<!-- no-compile -->
```csharp
// 1. Build the invoice domain model
IInvoice invoice = BuildInvoice(order);

// 2. Convert to UBL XML
XDocument ubl = new UblConverter().Convert(new UblDocumentInput { Invoice = invoice, Supplier = invoice.Supplier });

// 3. Transmit via Peppol
var result = await peppolService.Send(ubl);

// 4. (Optional) also create in Billit for accounting, then send the invoice it created
var created = await invoiceManager.Create(invoice);
await invoiceManager.Send(created.InvoiceId);
```

