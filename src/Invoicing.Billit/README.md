# Regira.Invoicing.Billit

Billit backend for [Regira Invoicing](https://regira.github.io/Regira-Packages/src/Common.Invoicing/). `services.AddBillit(...)`, configured with a `BillitConfig`, registers `IInvoiceManager`, which creates and sends the shared `IInvoice` model through Billit, alongside `IFileManager`, `IPartyManager` and `IPeppolManager`. It calls the Billit REST API directly over `HttpClient`.

## Installation

```xml
<PackageReference Include="Regira.Invoicing.Billit" Version="6.*" />
```

Requires a Billit account: `BillitConfig` takes your Billit party ID, the API base URL and an API key.

## Documentation

- [Billit](https://regira.github.io/Regira-Packages/src/Common.Invoicing/#billit) — `BillitConfig`, the DI registration and the `IInvoiceManager` contract
- [Typical end-to-end flow](https://regira.github.io/Regira-Packages/src/Common.Invoicing/#typical-end-to-end-flow) — Billit alongside UBL conversion and Peppol transmission

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
