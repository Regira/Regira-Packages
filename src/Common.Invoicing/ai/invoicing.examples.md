# Invoicing — Example: B2B Electronic Invoice

> Context: A wholesale distributor creates invoices in its own domain model, converts them to UBL XML, and transmits them via Peppol. Invoices are also pushed to Billit for accounting.

## DI Registration

```csharp
using Regira.Invoicing.Billit.Config;                // BillitConfig
using Regira.Invoicing.Billit.DependencyInjection;   // AddBillit

services.AddBillit(sp => new BillitConfig
{
    PartyId = configuration["Billit:PartyId"],
    Api     = new() { BaseUrl = configuration["Billit:Api:Url"], Key = configuration["Billit:Api:Key"] }
});
```

## Convert and transmit a Peppol invoice

<!-- no-compile -->
```csharp
public async Task SendPeppolInvoice(Order order)
{
    // 1. Build UBL XML from the invoice domain model
    IInvoice invoice = MapInvoice(order);   // your mapping: code, dates, supplier, customer, invoice lines
    var converter = new UblConverter();
    XDocument ubl = converter.Convert(new UblDocumentInput
    {
        Invoice  = invoice,
        Supplier = invoice.Supplier   // the converter reads the seller from here only, not from Invoice
    });

    // 2. Transmit via AdValVas
    var peppolService = new PeppolService(_gatewaySettings, _jsonSerializer);
    var result        = await peppolService.Send(ubl);

    if (!result.Success)
        throw new Exception($"Peppol transmission failed: {result.Reference}");
}
```

## Create and send via Billit

<!-- no-compile -->
```csharp
public async Task SendViaBillit(IInvoice invoice)
{
    var created = await _invoiceManager.Create(invoice);
    await _invoiceManager.Send(created.InvoiceId);
}
```
