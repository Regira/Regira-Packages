# Invoicing — Example: B2B Electronic Invoice

> Context: A wholesale distributor creates invoices in its own domain model, converts them to UBL XML, and transmits them via Peppol. Invoices are also pushed to Billit for accounting.

## DI Registration

```csharp
services.AddBillit(sp => new BillitConfig
{
    PartyId = configuration["Billit:PartyId"],
    Api     = new() { BaseUrl = configuration["Billit:Api:Url"], Key = configuration["Billit:Api:Key"] }
});
```

## Convert and transmit a Peppol invoice

```csharp
public async Task SendPeppolInvoice(Order order)
{
    // 1. Build UBL XML
    var converter = new UblConverter();
    XDocument ubl = converter.Convert(new UblDocumentInput
    {
        InvoiceNumber  = order.InvoiceNumber,
        IssueDate      = order.InvoiceDate,
        TypeCode       = InvoiceTypeCode.Commercial,
        SupplierParty  = MapParty(_myCompany),
        CustomerParty  = MapParty(order.Customer),
        Lines          = order.Lines.Select(MapLine).ToList(),
        TaxTotal       = new TaxTotal { TaxAmount = order.VatTotal, TaxCategoryCode = TaxCategoryCode.Standard }
    });

    // 2. Transmit via AdValVas
    var peppolService = new PeppolService(_gatewaySettings, _jsonSerializer);
    var result        = await peppolService.Send(ubl);

    if (!result.Success)
        throw new Exception($"Peppol transmission failed: {result.Reference}");
}
```

## Create and send via Billit

```csharp
public async Task SendViaBillit(IInvoice invoice)
{
    var created = await _invoiceManager.Create(invoice);
    await _invoiceManager.Send(created.Id!);
}
```
