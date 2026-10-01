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
```

---

## Billit

### `BillitConfig`

| Property | Type | Description |
|---|---|---|
| `PartyId` | `string?` | Your Billit party ID |
| `Api.BaseUrl` | `string?` | Billit API base URL |
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
// Registers: IInvoiceManager, IFileManager, IPartyManager, IPeppolManager
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

Produces a UBL 2.1 `Invoice` document.

```csharp
IInvoice invoice = new Invoice { /* lines, parties, tax, etc. */ };

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

### What the converter writes

Some fields are fixed rather than taken from the invoice:

- Customization ID and Profile ID from `UblConstants`: Peppol BIS Billing 3.0
- Invoice type code `380`, a commercial invoice, for every document. `IInvoice.InvoiceType` is not read, so the
  converter writes no credit notes
- Currency `EUR`
- Payment means `1` (not defined), with `RemittanceInfo` as the payment ID
- Tax category `S` (standard rate) on every line

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
var service = new PeppolService(gatewaySettings, jsonSerializer);

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

