# Regira.Payments.Pom

POM backend for [Regira Payments](https://regira.github.io/Regira-Packages/src/Common.Payments/). `PaymentService`, constructed with `PomSettings` and an `ISerializer`, implements the shared `IPaymentService` from `Regira.Invoicing`: `Save` creates a payment link for an `IPayment` and `Details` reads its status. It calls the POM REST API directly over `HttpClient`, with a token from the configured auth endpoint. Of the two payment backends it is the one that implements `IPaymentService`.

## Installation

```xml
<PackageReference Include="Regira.Payments.Pom" Version="6.*" />
```

Requires a POM account: `PomSettings` takes your sender ID, contract number, API username and password, and the POM endpoint URLs.

## Documentation

- [POM](https://regira.github.io/Regira-Packages/src/Common.Payments/#pom) — `PomSettings`, creating a payment link and reading its status, error handling
- [Notes](https://regira.github.io/Regira-Packages/src/Common.Payments/#notes) — where the shared payment contracts live and how the two backends differ

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
