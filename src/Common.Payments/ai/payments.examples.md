# Payments — Example: Webshop Checkout

> Context: A webshop creates a Mollie payment when a customer checks out and handles the webhook to mark the order as paid.

## DI Registration

Mollie's `PaymentService` implements no interface — register and inject the concrete class.

```csharp
using Regira.Payments.Mollie.Config;   // MollieConfig

services.AddSingleton(_ => new Regira.Payments.Mollie.Services.PaymentService(
    new MollieConfig
    {
        Key             = configuration["Mollie:Key"]!,
        // the factories run before Mollie assigns the payment's Id: build on the app's own reference in Metadata
        RedirectFactory = p => $"https://myshop.com/checkout/return/{p.Metadata!["orderId"]}",
        WebhookFactory  = _ => "https://myshop.com/checkout/webhook"
    }));
```

The `_paymentService` field below is the injected `Regira.Payments.Mollie.Services.PaymentService`.

## Create a payment at checkout

<!-- no-compile -->
```csharp
public async Task<string> StartCheckout(Order order)
{
    var payment = new Payment // Regira.Invoicing.Payments.Models
    {
        Amount      = order.Total,
        Currency    = "EUR",
        Description = $"Order #{order.Number}",
        Metadata    = new Dictionary<string, object?> { ["orderId"] = order.Number }
    };

    var response = await _paymentService.Save(payment); // sets payment.Id
    return response.CheckoutUrl!;                       // redirect customer here
}
```

## Handle the Mollie webhook

<!-- no-compile -->
```csharp
// Mollie posts the payment id as the form field "id"; WebHook reads the payment back from Mollie
[HttpPost("checkout/webhook")]
public async Task<IActionResult> Webhook([FromForm] string id)
{
    await _paymentService.WebHook(id, async p =>
    {
        if (p?.Status == PaymentStatus.Paid)
            await _orderService.MarkPaid(p.Metadata!["orderId"]!.ToString()!);
    });
    return Ok();
}
```

## Check payment status

<!-- no-compile -->
```csharp
public async Task<bool> IsPaid(string paymentId)
{
    var payment = await _paymentService.Details(paymentId);
    return payment?.Status == PaymentStatus.Paid;
}
```
