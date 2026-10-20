# Invoicing — Example: B2B Electronic Invoice

> Context: A wholesale distributor creates invoices in its own domain model and transmits them via Peppol: as UBL XML through AdValVas, or through Billit, which delivers over Peppol as well. Each invoice goes out one way only.

## DI Registration

```csharp
using Regira.Invoicing.Billit.Config;                // BillitConfig
using Regira.Invoicing.Billit.DependencyInjection;   // AddBillit

services.AddBillit(sp => new BillitConfig
{
    PartyId = configuration["Billit:PartyId"],
    Api     = new() { BaseUrl = configuration["Billit:Api:Url"], Key = configuration["Billit:Api:Key"] }
});
// the ISerializer Billit's managers take, which AddBillit does not register
services.AddSingleton<Regira.Serializing.Abstractions.ISerializer, Regira.Serializing.Newtonsoft.Json.JsonSerializer>();
```

## Convert and transmit a Peppol invoice

<!-- no-compile -->
```csharp
public async Task SendPeppolInvoice(Order order)
{
    // 1. Build UBL XML from the invoice domain model
    IInvoice invoice = MapInvoice(order);   // your mapping: code, dates, invoice lines, and both parties with
                                            // a Peppol identifier and an address (see How the model maps to UBL)
    var converter = new UblConverter();
    XDocument ubl = converter.Convert(new UblDocumentInput
    {
        Invoice  = invoice,
        Supplier = invoice.Supplier   // the converter reads the seller from here only, not from Invoice
    });

    // 2. Transmit via AdValVas. Send returns once the gateway accepted the document; otherwise it throws
    //    PeppolRequestException (a non-2xx answer) or PeppolResponseException (the document was not accepted).
    //    The serializer reads the gateway's answer; the request always carries the gateway's PascalCase names.
    var peppolService = new PeppolService(_gatewaySettings, _jsonSerializer);
    await peppolService.Send(ubl);
}
```

## Or: create and send via Billit

<!-- no-compile -->
```csharp
public async Task SendViaBillit(IInvoice invoice)
{
    // instead of AdValVas, not as well: both Billit sends deliver over Peppol. Send(invoice) creates the
    // invoice in Billit, then sends it; Send(ids) sends one created before
    await _invoiceManager.Send(invoice);
}
```
