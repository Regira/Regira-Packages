# Regira.Payments.Mollie

Mollie backend for [Regira Payments](https://regira.github.io/Regira-Packages/src/Common.Payments/), built on the [Mollie.Api](https://www.nuget.org/packages/Mollie.Api) client. `PaymentService`, configured with a `MollieConfig`, creates, reads, lists and cancels payments as the shared `IPayment` model from `Regira.Invoicing`, and handles Mollie's webhook callback. Unlike the POM backend it does not implement `IPaymentService`: it is a standalone class with a richer surface (`List`, `Delete`, `WebHook`, and a `Save` that returns the checkout URL).

## Installation

```xml
<PackageReference Include="Regira.Payments.Mollie" Version="6.*" />
```

Requires a Mollie account: `MollieConfig.Key` takes its API key (`test_...` or `live_...`).

## Documentation

- [Mollie](https://regira.github.io/Regira-Packages/src/Common.Payments/#mollie) — `MollieConfig`, the `PaymentService` calls and webhook handling
- [Notes](https://regira.github.io/Regira-Packages/src/Common.Payments/#notes) — where the shared payment contracts live and how the two backends differ

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
