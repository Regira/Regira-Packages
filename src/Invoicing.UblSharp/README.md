# Regira.Invoicing.UblSharp

UBL converter for [Regira Invoicing](https://regira.github.io/Regira-Packages/src/Common.Invoicing/), built on [UblSharp](https://www.nuget.org/packages/UblSharp). `UblConverter` implements `IUblConverter` and turns the shared `IInvoice` model, passed in a `UblDocumentInput`, into a UBL 2.1 `Invoice` document (`XDocument`) carrying the Peppol BIS Billing 3.0 customization and profile IDs.

## Installation

```xml
<PackageReference Include="Regira.Invoicing.UblSharp" Version="6.*" />
```

The package only builds the XML. Delivering it over Peppol goes through an access point, such as `Regira.Invoicing.ViaAdValvas`.

## Documentation

- [UblSharp — UBL Conversion](https://regira.github.io/Regira-Packages/src/Common.Invoicing/#ublsharp--ubl-conversion) — `IUblConverter`, `UblDocumentInput` (the seller goes in its `Supplier`), and the fields the converter writes as fixed values: type code, currency, payment means and tax category
- [Typical end-to-end flow](https://regira.github.io/Regira-Packages/src/Common.Invoicing/#typical-end-to-end-flow) — conversion followed by Peppol transmission

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
