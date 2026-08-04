# Payments — Example: Webshop Checkout

> Context: A webshop creates a Mollie payment when a customer checks out and handles the webhook to mark the order as paid.

## DI Registration

Mollie's `PaymentService` implements no interface — register and inject the concrete class.

```csharp
services.AddSingleton(_ => new Regira.Payments.Mollie.Services.PaymentService(
    new MollieConfig
    {
        Api             = "https://api.mollie.com/v2",
        Key             = configuration["Mollie:Key"]!,
        RedirectFactory = p => $"https://myshop.com/checkout/return/{p.Id}",
        WebhookFactory  = p => $"https://myshop.com/checkout/webhook/{p.Id}"
    }));
```

The `_paymentService` field below is the injected `Regira.Payments.Mollie.Services.PaymentService`.

## Create a payment at checkout

```csharp
public async Task<string> StartCheckout(Order order)
{
    var payment = new Payment // Regira.Invoicing.Payments.Models
    {
        Amount      = order.Total,
        Currency    = "EUR",
        Description = $"Order #{order.Number}"
    };

    var response = await _paymentService.Save(payment); // sets payment.Id
    return response.CheckoutUrl!;                       // redirect customer here
}
```

## Handle the Mollie webhook

```csharp
[HttpPost("checkout/webhook/{orderId}")]
public async Task<IActionResult> Webhook(string orderId)
{
    await _paymentService.WebHook(orderId, async p =>
    {
        if (p?.Status == PaymentStatus.Paid)
            await _orderService.MarkPaid(orderId);
    });
    return Ok();
}
```

## Check payment status

```csharp
public async Task<bool> IsPaid(string paymentId)
{
    var payment = await _paymentService.Details(paymentId);
    return payment?.Status == PaymentStatus.Paid;
}
```
