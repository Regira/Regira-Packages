# Regira Invoicing

Regira Invoicing covers electronic invoice creation, UBL/Peppol conversion, and document transmission via an AP gateway.

## Projects

| Project | Package | Purpose |
|---------|---------|---------|
| `Common.Invoicing` | `Regira.Invoicing` | Shared abstractions |
| `Invoicing.Billit` | `Regira.Invoicing.Billit` | Create and send invoices via Billit |
| `Invoicing.UblSharp` | `Regira.Invoicing.UblSharp` | Convert invoices to UBL XML (Peppol BIS) |
| `Invoicing.ViaAdValvas` | `Regira.Invoicing.ViaAdValvas` | Transmit UBL documents via AdValVas AP gateway |

## Installation

```xml
<PackageReference Include="Regira.Invoicing.Billit"       Version="6.*" />
<PackageReference Include="Regira.Invoicing.UblSharp"     Version="6.*" />
<PackageReference Include="Regira.Invoicing.ViaAdValvas"  Version="6.*" />
```

---

## Billit

### BillitConfig

| Property | Type | Description |
|----------|------|-------------|
| `PartyId` | `string?` | Your Billit party ID |
| `Api.BaseUrl` | `string?` | Billit API base URL |
| `Api.Key` | `string?` | API key |

### DI Registration

```csharp no-compile
services.AddBillit(sp => new BillitConfig
{
    PartyId = configuration["Billit:PartyId"],
    Api     = new() { BaseUrl = configuration["Billit:Api:Url"], Key = configuration["Billit:Api:Key"] }
});
// Registers: IInvoiceManager, IFileManager, IPartyManager, IPeppolManager
```

### IInvoiceManager

```csharp no-compile
Task<ICreateInvoiceResult> Create(IInvoice item);
Task<ISendInvoiceResult>   Send(params string[] ids);    // send by IDs
Task<ISendInvoiceResult>   Send(IInvoice input);          // send by invoice object
```

---

## UblSharp — UBL Conversion

### IUblConverter

```csharp no-compile
XDocument Convert(UblDocumentInput input);
```

Produces a UBL 2.1 `Invoice` document.

```csharp
IInvoice invoice = new Invoice { /* lines, parties, tax, etc. */ };

var converter = new UblConverter();
XDocument ubl = converter.Convert(new UblDocumentInput
{
    Invoice = invoice   // required
});
```

### Supporting constants

- `UblConstants` — Customization ID and Profile ID for Peppol BIS Billing 3.0
- `InvoiceTypeCode` — e.g. `380` (`Commercial`), `383` (`DebitNote`); credit notes have no type code — they are distinguished by the UBL root element name
- `PaymentMeansCode` — `1` (`NotDefined`), `42` (`BankAccount`), `ZZZ` (`MutuallyDefined`)
- `TaxCategoryCode` — `S` (standard), `Z` (zero-rated), `E` (exempt), `AE` (reverse charge)

---

## ViaAdValvas — Peppol Transmission

### GatewaySettings

| Property | Type | Description |
|----------|------|-------------|
| `Uri` | `string` | AdValVas gateway endpoint |
| `SenderID` | `string` | Your Peppol participant ID |
| `SenderName` | `string` | Display name |
| `Token` | `string` | API token |
| `SecretKey` | `string` | Secret key included in the request seal |
| `IsProduction` | `bool` | Target the production gateway (default `false`) |

### PeppolService

```csharp no-compile
var service = new PeppolService(gatewaySettings, jsonSerializer);

UblDocumentResponse result = await service.Send(ublDocument);

if (result.Success)
    Console.WriteLine($"Sent. Reference: {result.Reference}");
```

Requests are sealed with `SealUtility.Generate()` — an MD5 digest over the token, sender ID, reference ID, date, and the secret key.

---

## Typical end-to-end flow

```csharp no-compile
// 1. Build the invoice domain model
IInvoice invoice = BuildInvoice(order);

// 2. Convert to UBL XML
XDocument ubl = new UblConverter().Convert(new UblDocumentInput { Invoice = invoice });

// 3. Transmit via Peppol
var result = await peppolService.Send(ubl);

// 4. (Optional) also create in Billit for accounting
await invoiceManager.Create(invoice);
await invoiceManager.Send(invoice);
```
