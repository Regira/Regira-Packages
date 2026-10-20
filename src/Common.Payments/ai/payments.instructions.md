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

```bash
# Mollie payment gateway
dotnet add package Regira.Payments.Mollie

# POM payment gateway
dotnet add package Regira.Payments.Pom
```

---

## Shared Abstraction

The shared contracts ship in the `Regira.Invoicing` package: `IPayment` and `IPaymentService` in `Regira.Invoicing.Payments.Abstractions`, the `Payment` model in `Regira.Invoicing.Payments.Models`, `PaymentStatus` in `Regira.Invoicing.Payments.Enums`.

- **POM**: `PaymentService` implements `IPaymentService` (`Details`, `Save`) — register and consume it via the interface. Only its own `CreatePaylink` returns the payment link, so inject the concrete class where the payer needs the link.
- **Mollie**: `PaymentService` implements no interface — register and consume the concrete class. It offers `Details`, `List`, `Save` (returns `CreatePaymentResponse` with the checkout URL), `Delete` (cancels) and `WebHook`.

Both accept and return the shared `IPayment` model and can be registered as singletons.

---

## Mollie

### `MollieConfig`

| Property | Type | Description |
|---|---|---|
| `Api` | `string` | Not read: the Mollie client calls Mollie's API itself |
| `Key` | `string` | Mollie API key. A `test_…` key is test mode — there is no separate switch; a `live_…` key takes real payments |
| `MaxPageSize` | `int` | Default `250` |
| `RedirectFactory` | `Func<IPayment, string>?` | Builds redirect URL per payment. It runs before Mollie assigns the payment's `Id`, so build it from the app's own reference in `Metadata` |
| `WebhookFactory` | `Func<IPayment, string>?` | Builds webhook URL per payment, before its `Id` is set too. Mollie posts the payment id to it as the form field `id` |

### `PaymentService` (Mollie)

<!-- no-compile -->
```csharp
var svc = new Regira.Payments.Mollie.Services.PaymentService(new MollieConfig
{
    Key             = configuration["Mollie:Key"]!,
    RedirectFactory = p => $"https://myapp.com/payment/return/{p.Metadata!["orderId"]}",
    WebhookFactory  = _ => "https://myapp.com/payment/webhook"
});

IPayment?              payment = await svc.Details(paymentId);
IEnumerable<IPayment>  list    = await svc.List();
newPayment.Metadata = new Dictionary<string, object?> { ["orderId"] = orderId }; // the factories above read it
var response = await svc.Save(newPayment); // creates payment, sets newPayment.Id
var checkoutUrl = response.CheckoutUrl;    // redirect the customer here
// or Save(newPayment, orderId): a repeat with the same idempotency key returns the first payment
await svc.Delete(payment!);                // cancels the payment
```

Every instance sends through a connection pool the package shares, so it can be registered with any lifetime. Never repeat a `Save` without an idempotency key: the second creates a second payment.

### Webhook Handling

<!-- no-compile -->
```csharp
// paymentId: the form field "id" Mollie posts to the webhook URL; WebHook reads the payment back from Mollie
await svc.WebHook(paymentId, async p =>
{
    if (p?.Status == PaymentStatus.Paid)
        await orderService.MarkPaid(p.Metadata!["orderId"]!.ToString()!);
});
```

---

## POM

### `PomSettings`

| Property | Type | Description |
|---|---|---|
| `SenderId` | `string?` | POM sender ID |
| `SenderContractNumber` | `string?` | Contract number, sent with every payment |
| `Username` | `string?` | API username |
| `Password` | `string?` | API password |
| `AppName` | `string?` | Sent when authenticating |
| `Platform` | `string?` | Sent when authenticating |
| `EnvironmentId` | `string?` | Sent when authenticating |
| `ExpiresIn` | `int` | Sent with the authentication request. It does not set a link's expiry: `Payment.ExpiresAt` does |
| `Mode` | `string?` | `"live"` or `"test"` |
| `AuthApi` | `string?` | Authentication endpoint |
| `CreatePaylinkApi` | `string` | Create payment link endpoint |
| `PaylinkStatusApi` | `string` | Payment status endpoint, with a `{paymentId}` placeholder |
| `WebhookKey` | `string?` | Not read by `PaymentService` |

There is no `Add…` extension, configuration section or default endpoint. Take `AuthApi`, `CreatePaylinkApi` and
`PaylinkStatusApi` from POM, bind a section yourself, and register the service as a singleton, under its class for
`CreatePaylink` and under `IPaymentService`:
`services.AddSingleton(new Regira.Payments.Pom.PaymentService(configuration.GetSection("Payments:Pom").Get<PomSettings>()!, serializer));`
then `services.AddSingleton<IPaymentService>(sp => sp.GetRequiredService<Regira.Payments.Pom.PaymentService>());`.
Authentication posts the whole `PomSettings` object to `AuthApi`.

### `PaymentService` (POM)

<!-- no-compile -->
```csharp
// jsonSerializer: Regira.Serializing.Abstractions.ISerializer
IPaymentService svc = new Regira.Payments.Pom.PaymentService(pomSettings, jsonSerializer);

IPayment? existing = await svc.Details(paymentId);

var payment = new Payment // Regira.Invoicing.Payments.Models
{
    Amount    = 49.99m,
    Currency  = "EUR",
    ExpiresAt = DateTime.Now.AddDays(30)   // the link's expiry; one day after it is created when unset
};
await svc.Save(payment); // creates a payment link, sets payment.Id
```

To send the payer to the link, call `CreatePaylink` on the concrete class instead of `Save`:

<!-- no-compile -->
```csharp
var pom = new Regira.Payments.Pom.PaymentService(pomSettings, jsonSerializer);
var response = await pom.CreatePaylink(payment); // sets payment.Id
var paylink = response.Paylink;                  // send the payer here
```

`Save` and `CreatePaylink` send `Amount`, `Currency` and `ExpiresAt` (the link's expiry, one day after it is created when unset; `ExpiresAt` is settable on `Payment`, and `IPayment` reads `null` for an implementation without it), the sender contract number coming from `PomSettings`, and set the payment's `Id` to POM's document id. They send nothing else: `Description` and `Metadata` stay behind, so a POM payment carries no reference of yours — keep POM's id on your own record; `CreatePaylink` also returns POM's `PomPaymentResponse`, with the link in `Paylink`.
Every instance sends through a connection pool the package shares. An instance keeps its POM token and, when POM refuses it (`401`), authenticates again and sends the request once more, so it can be registered as a singleton.
`PomException` is thrown on API errors — check `StatusCode` and `PomResponse` for details.

---

## Provider Comparison

| Feature | Mollie | POM |
|---|---|---|
| `IPaymentService` | — (concrete class) | ✓ |
| Operations | `Details`, `List`, `Save`, `Delete` | `Details`, `Save`, `CreatePaylink` |
| Webhook handling | `WebHook()` | — (not implemented: poll `Details(id)`) |
| Payment link | ✓ (`Save` returns checkout URL) | ✓ (`CreatePaylink` returns `Paylink`) |
| Client library | Official `Mollie.Api` NuGet | `HttpClient` + token auth (`X-Authentication`) |

---
