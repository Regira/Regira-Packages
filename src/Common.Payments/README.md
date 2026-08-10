# Regira Payments

Regira Payments provides payment processing via Mollie and POM, built on the shared payment contracts from `Regira.Invoicing`.

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `Common.Payments` | `Regira.Payments` | Packaging only (build targets, AI docs — no code) |
| `Payments.Mollie` | `Regira.Payments.Mollie` | Mollie API |
| `Payments.Pom` | `Regira.Payments.Pom` | POM payment gateway |

## Installation

```xml
<PackageReference Include="Regira.Payments.Mollie" Version="6.*" />
<PackageReference Include="Regira.Payments.Pom"    Version="6.*" />
```

---

## Mollie

### MollieConfig

| Property | Type | Description |
|----------|------|-------------|
| `Api` | `string` | Mollie API base URL — currently not consumed (the underlying `PaymentClient` is constructed from `Key` only) |
| `Key` | `string` | Mollie API key (`test_...` or `live_...`) |
| `MaxPageSize` | `int` | Default `250` |
| `RedirectFactory` | `Func<IPayment, string>?` | Builds redirect URL per payment |
| `WebhookFactory` | `Func<IPayment, string>?` | Builds webhook URL per payment |

### PaymentService

```csharp no-compile
var svc = new Regira.Payments.Mollie.Services.PaymentService(new MollieConfig
{
    Api             = "https://api.mollie.com/v2",
    Key             = configuration["Mollie:Key"]!,
    RedirectFactory = p => $"https://myapp.com/payment/return/{p.Id}",
    WebhookFactory  = p => $"https://myapp.com/payment/webhook/{p.Id}"
});

// CRUD
IPayment?             payment  = await svc.Details(paymentId);
IEnumerable<IPayment> list     = await svc.List();
var response = await svc.Save(newPayment); // creates payment, sets newPayment.Id
var checkoutUrl = response.CheckoutUrl;    // redirect the customer here
await svc.Delete(payment!);                // cancels the payment

// Handle webhook
await svc.WebHook(paymentId, async p =>
{
    if (p?.Status == PaymentStatus.Paid)
        await orderService.MarkPaid(p.Id);
});
```

---

## POM

### PomSettings

| Property | Type | Description |
|----------|------|-------------|
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

### PaymentService

```csharp
var pomSettings = new PomSettings { /* sender + API credentials, endpoints */ };
ISerializer jsonSerializer = new JsonSerializer(); // Regira.Serializing.Newtonsoft.Json
var svc = new Regira.Payments.Pom.PaymentService(pomSettings, jsonSerializer);

// Get payment status
IPayment? existing = await svc.Details("pom-payment-id");

// Create a payment link
var payment = new Payment // Regira.Invoicing.Payments.Models
{
    Amount   = 49.99m,
    Currency = "EUR"
};
await svc.Save(payment);
```

`Save` sends `Amount` and `Currency`; the sender contract number comes from `PomSettings`.
`PomException` is thrown on API errors; check `StatusCode` and `PomResponse` for details.

---

## Notes

- Both services can be registered as singletons.
- The shared contracts (`IPayment`, `IPaymentService`, the `Payment` model, `PaymentStatus`) ship in the `Regira.Invoicing` package, under `Regira.Invoicing.Payments.Abstractions`, `.Models` and `.Enums`.
- POM's `PaymentService` implements `IPaymentService`; Mollie's is a standalone class with a richer surface (`List`, `Delete`, `WebHook`, and `Save` returning `CreatePaymentResponse`).
- Mollie uses the official `Mollie.Api` NuGet client; POM calls its REST API via `HttpClient` with a token from the auth endpoint (`X-Authentication` header).

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
