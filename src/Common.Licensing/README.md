# Regira.Licensing

Offline license-key validation for the commercially licensed Regira packages. A key is an RSA-signed token verified against a public key embedded in the package, with no network call. `services.UseRegira(configuration)` (reads `Regira:LicenseKeys`) or `services.UseRegira(licenseKey)` registers each key as a `License`, writes its settings to the console and reminds you when it nears expiry; the licensed packages then take the best license per product and validate it with `LicenseValidator`. With no key registered, the free tier applies.

## Installation

```xml
<PackageReference Include="Regira.Licensing" Version="6.*" />
```

Regira.Entities.DependencyInjection and Regira.Office.Clients depend on this package, so an application usually receives it transitively. `UseRegira` lives in the `Regira.Licensing.DependencyInjection` namespace; call it before the licensed module's setup.

## Documentation

- [Licensing](https://regira.github.io/Regira-Packages/licensing.html) — which packages are licensed, the free-tier limits, trial and commercial keys, and expiry
- [Registering a key](https://regira.github.io/Regira-Packages/licensing.html#registering-a-key) — `UseRegira` from configuration or with explicit keys

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits and is validated fully offline. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
