# Regira Payments AI Agent Instructions

> Payment processing for the Mollie and POM gateways, built on the shared `IPayment` model from `Regira.Invoicing`. POM's `PaymentService` implements `IPaymentService`; Mollie's is a standalone class with a richer API.

## Projects

| Project | Package | Backend |
|---|---|---|
| `Common.Payments` | `Regira.Payments` | Packaging only (build targets, AI docs — no code) |
| `Payments.Mollie` | `Regira.Payments.Mollie` | Mollie API |
| `Payments.Pom` | `Regira.Payments.Pom` | POM payment gateway |

---

## Installation

```xml
<!-- Mollie payment gateway -->
<PackageReference Include="Regira.Payments.Mollie" Version="6.*" />

<!-- POM payment gateway -->
<PackageReference Include="Regira.Payments.Pom" Version="6.*" />
```

---

## Shared Abstraction

The shared contracts ship in the `Regira.Invoicing` package: `IPayment` and `IPaymentService` in `Regira.Invoicing.Payments.Abstractions`, the `Payment` model in `Regira.Invoicing.Payments.Models`, `PaymentStatus` in `Regira.Invoicing.Payments.Enums`.

- **POM**: `PaymentService` implements `IPaymentService` (`Details`, `Save`) — register and consume it via the interface.
- **Mollie**: `PaymentService` implements no interface — register and consume the concrete class. It offers `Details`, `List`, `Save` (returns `CreatePaymentResponse` with the checkout URL), `Delete` (cancels) and `WebHook`.

Both accept and return the shared `IPayment` model and can be registered as singletons.

---

## Mollie

### `MollieConfig`

| Property | Type | Description |
|---|---|---|
| `Api` | `string` | Mollie API base URL |
| `Key` | `string` | Mollie API key (`test_...` or `live_...`) |
| `MaxPageSize` | `int` | Default `250` |
| `RedirectFactory` | `Func<IPayment, string>?` | Builds redirect URL per payment |
| `WebhookFactory` | `Func<IPayment, string>?` | Builds webhook URL per payment |

### `PaymentService` (Mollie)

```csharp
var svc = new Regira.Payments.Mollie.Services.PaymentService(new MollieConfig
{
    Api             = "https://api.mollie.com/v2",
    Key             = configuration["Mollie:Key"]!,
    RedirectFactory = p => $"https://myapp.com/payment/return/{p.Id}",
    WebhookFactory  = p => $"https://myapp.com/payment/webhook/{p.Id}"
});

IPayment?              payment = await svc.Details(paymentId);
IEnumerable<IPayment>  list    = await svc.List();
var response = await svc.Save(newPayment); // creates payment, sets newPayment.Id
var checkoutUrl = response.CheckoutUrl;    // redirect the customer here
await svc.Delete(payment!);                // cancels the payment
```

### Webhook Handling

```csharp
await svc.WebHook(paymentId, async p =>
{
    if (p?.Status == PaymentStatus.Paid)
        await orderService.MarkPaid(p.Id);
});
```

---

## POM

### `PomSettings`

| Property | Type | Description |
|---|---|---|
| `SenderId` | `string?` | POM sender ID |
| `SenderContractNumber` | `string?` | Contract number |
| `Username` | `string?` | API username |
| `Password` | `string?` | API password |
| `ExpiresIn` | `int` | Payment link expiry (seconds) |
| `Mode` | `string?` | `"live"` or `"test"` |
| `AuthApi` | `string?` | Authentication endpoint |
| `CreatePaylinkApi` | `string` | Create payment link endpoint |
| `PaylinkStatusApi` | `string` | Payment status endpoint |
| `WebhookKey` | `string?` | Webhook HMAC key |

### `PaymentService` (POM)

```csharp
// jsonSerializer: Regira.Serializing.Abstractions.ISerializer
IPaymentService svc = new Regira.Payments.Pom.PaymentService(pomSettings, jsonSerializer);

IPayment? existing = await svc.Details(paymentId);

var payment = new Payment // Regira.Invoicing.Payments.Models
{
    Amount   = 49.99m,
    Currency = "EUR"
};
await svc.Save(payment);
```

`Save` sends `Amount` and `Currency`; the sender contract number comes from `PomSettings`.
`PomException` is thrown on API errors — check `StatusCode` and `PomResponse` for details.

---

## Provider Comparison

| Feature | Mollie | POM |
|---|---|---|
| `IPaymentService` | — (concrete class) | ✓ |
| Operations | `Details`, `List`, `Save`, `Delete` | `Details`, `Save` |
| Webhook handling | `WebHook()` | Via `WebhookKey` HMAC |
| Payment link | ✓ (`Save` returns checkout URL) | ✓ (`CreatePaylinkApi`) |
| Client library | Official `Mollie.Api` NuGet | `HttpClient` + token auth (`X-Authentication`) |

---
