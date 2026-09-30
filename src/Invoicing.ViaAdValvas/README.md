# Regira.Invoicing.ViaAdValvas

Peppol transmission for [Regira Invoicing](https://regira.github.io/Regira-Packages/src/Common.Invoicing/) through the AdValVas access point gateway. `PeppolService`, constructed with `GatewaySettings` and an `ISerializer`, posts a UBL `Invoice` or `CreditNote` (`XDocument`) to the gateway, seals each request with `SealUtility`, and returns a `UblDocumentResponse` carrying the message reference. It calls the gateway directly over `HttpClient`.

## Installation

```xml
<PackageReference Include="Regira.Invoicing.ViaAdValvas" Version="6.*" />
```

Requires access to the AdValVas gateway: `GatewaySettings` takes the gateway endpoint, your Peppol participant ID and sender name, an API token and a secret key.

## Documentation

- [ViaAdValvas — Peppol Transmission](https://regira.github.io/Regira-Packages/src/Common.Invoicing/#viaadvalvas--peppol-transmission) — `GatewaySettings`, sending a document and how requests are sealed
- [Typical end-to-end flow](https://regira.github.io/Regira-Packages/src/Common.Invoicing/#typical-end-to-end-flow) — UBL conversion followed by transmission

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
