# Regira.Office.Clients

HTTP clients for the hosted Regira Office API, so an application uses [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) services without hosting their backends. `AddOfficeClients` registers typed `HttpClient` implementations of the Office interfaces, pointed at `OfficeClientOptions.BaseUrl`: HTML and images to PDF, PDF merge, split, text and page images, Word creation, conversion, merging and text extraction, Excel, CSV, barcodes and QR codes, OCR, and mail-message parsing. Every call carries the application's `regira.services` license key, registered with `UseRegira`, in the `X-License-Key` header; `ILicenseStatusClient.GetStatus()` asks the API what it makes of that key.

## Installation

```xml
<PackageReference Include="Regira.Office.Clients" Version="6.*" />
```

Without a key, calls fall under the rate-limited free tier of the hosted services.

## Documentation

- [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) — the Office modules and the interfaces these clients implement
- [Licensing](https://regira.github.io/Regira-Packages/licensing.html) — the free-tier limits, registering a key with `UseRegira`, and checking it with `ILicenseStatusClient`
- [Word through the Office API](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#office-api) — what `WordClient` sends, and the page settings the API's conversion does not take: orientation and margins
- [Word.Gotenberg beside the Office clients](https://regira.github.io/Regira-Packages/src/Common.Office/docs/word/#wordgotenberg) — converting Word documents with a Gotenberg server while the Office API serves the rest

## License

Regira Commercial License, with a free tier that applies automatically — no key is needed within its limits. A license key removes the limits; the hosted service checks the key each call carries. See the [license text](https://github.com/Regira/Regira-Packages/blob/main/legal/REGIRA-COMMERCIAL-LICENSE.md) and the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html); keys are available at [regira.com/licensing](https://regira.com/licensing).
