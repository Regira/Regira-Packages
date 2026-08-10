# Licensing

Regira packages use a split licensing model designed to stay out of your way: **most packages are Apache-2.0**, and the handful that fund the project carry a commercial license with a built-in free tier.

## At a glance

| Packages | License | Key needed? |
|----------|---------|-------------|
| Everything except the six below | [Apache License 2.0](LICENSE) | Never — these packages contain **no license validation and no runtime limits** |
| `Regira.Licensing`, `Regira.Entities.DependencyInjection`, `Regira.Entities.Web`, `Regira.Entities.Mapping.Mapster`, `Regira.Entities.Mapping.AutoMapper`, `Regira.Office.Clients` | [Regira Commercial License](legal/REGIRA-COMMERCIAL-LICENSE.md) | Only beyond the free tier |

The front-end library [`@regira/modules`](https://github.com/Regira/Regira-Modules) (npm) is also Apache-2.0. The hosted services ([services.regira.com](https://services.regira.com/)) and the [MCP server](https://mcp.regira.com/mcp) follow the commercial model with rate-limited free tiers.

## The free tier

The free tier applies automatically — no payment, no registration, no key:

| Product | Free-tier limit |
|---------|-----------------|
| `regira.entities` | 5 **simple** + 2 **complex** entity registrations per application |
| `regira.services` | 5 requests per 60 seconds |
| `regira.mcp` | 30 requests per 60 seconds |

A registration is **simple** when it is made without custom sort or include type parameters (`For<Product>()`, `For<Product, int, ProductSearchObject>(...)`), and **complex** when it specifies them (`For<Order, int, OrderSearchObject, OrderSortBy, OrderIncludes>(...)`).

For any given released version, the free tier as shipped in that version remains available without time limit.

## Commercial licenses

A license key removes the free-tier limits. Keys are validated **fully offline** with an RSA-signed token — no phone-home, no telemetry (the validation code is public: [`Regira.Licensing`](src/Common.Licensing)).

- **Trial** — 30 days, all features, no payment required.
- **Commercial** — annual, per developer: currently **€99/yr** (Regira Entities) and **€49/yr** (Regira Services), MCP server included. Canonical prices and purchase: [regira.com/licensing](https://regira.com/licensing). Keys are emailed immediately.

One key can cover multiple products; when several keys are registered, the best license per product wins (paid always beats free).

## Registering a key

Register once, before module setup:

```csharp
services.UseRegira(configuration);   // reads Regira:LicenseKeys from configuration
// or
services.UseRegira(licenseKey);      // pass keys explicitly
```

```json
{
  "Regira": {
    "LicenseKeys": [ "<your-license-key>" ]
  }
}
```

## FAQ

- **Is the free tier free forever?** Yes — per released version, the shipped free tier is not revocable. Future versions may adjust limits.
- **What happens when a key expires?** Validation falls back to the free tier; nothing fails closed.
- **Can I ship Regira DLLs inside my commercial product?** Yes — the commercial license grants redistribution in compiled form as part of your application ([clause 3](legal/REGIRA-COMMERCIAL-LICENSE.md)). The Apache-2.0 packages carry the standard Apache grant.
- **Will license scanners flag Regira?** The Apache-2.0 packages carry a standard SPDX expression that every scanner recognizes. Only the six commercial packages show a custom license file.

Questions: [b2b@regira.com](mailto:b2b@regira.com) or the [contact form](https://regira.com/contact).
