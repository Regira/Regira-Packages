# Shared Setup for Regira AI Instructions

> **Role:** Load this file for setup rules reused by multiple module guides and for consumer-guide sync or distribution mechanics.
>
> **Boundaries:** Use the consumer bootstrap — `get_bootstrap_guide(heading: "toc")`, or its copy in `.regira/bootstrap.md` — for module discovery, top-level routing, and the execution-oriented consumer workflow. Use the project setup guide — `get_package(id: "Regira.Setup", section: "project.setup")` — for project-template choice and baseline app shape.

Use this file when a module guide needs shared setup without repeating it.

## Consumer Local Guides

- **Source repository**: the full `ai/` folder is available locally, so this file can point to adjacent guides.
- **Consumer project**: the normal entrypoint is the consumer bootstrap over the MCP, and `./AGENTS.md` is the app's own file (bootstrap → *The app's own AGENTS.md*); extracted `.regira/instructions/*.md` files are optional local guidance.

Keep these responsibilities separate in consumer projects:

- The consumer bootstrap decides project template, module selection, package routing, and when extracted local guides must be read before code generation.
- `./AGENTS.md` records what was decided for this app: its packages, layout, conventions and deviations. The app writes and maintains it.
- `.regira/instructions/*.md` are optional local guides created by package extraction.
- `Regira.Setup` is the package path for extracting the shared setup guides `project.setup.md` and `shared.setup.md` during build.
- Module packages that ship AI files can extract their own module-specific guides during build through their package targets.
- The `project.setup` section (`get_package(id: "Regira.Setup", section: "project.setup")`) stays focused on template selection and baseline app shape.
- `shared.setup.md` stays focused on setup rules reused across modules and local guide extraction behavior.

Consumers do not need the source-repository `ai/` folder for the normal one-file flow. Install `Regira.Setup` when the shared setup guides should be extracted locally through package restore and build.

## Outbound HTTP Clients

- **ViaAdValvas, GitHub storage, Mollie, POM, SendGrid and Mailgun** send through one connection pool per package, recycled every two minutes so a DNS change reaches a long-running process. It keeps no cookies, so nothing one instance receives rides on another's requests. Any lifetime is safe, a singleton included, and `HttpClient`'s 100-second timeout applies; there is nothing to configure.
- **Gotenberg, Billit and Office.Clients** register named clients. Their timeout, handlers and resilience belong to the app, on that name:

| Package | Client name |
|---|---|
| PDF.Gotenberg, Word.Gotenberg | `HttpClientName` on the registration's extension class |
| Invoicing.Billit | `BillitConstants.HttpClientName` |
| Office.Clients | each client's interface name, `nameof(IWordConverter)` |

<!-- no-compile -->
```csharp
services.AddHttpClient(BillitConstants.HttpClientName)
    .AddStandardResilienceHandler(o => o.Retry.DisableForUnsafeHttpMethods());
```

- `AddStandardResilienceHandler()` (`Microsoft.Extensions.Http.Resilience`) retries POSTs by default. A call that creates something must not repeat, so disable retries for unsafe methods, as above.
- An app's own retry loop never repeats a create either. Mollie's `Save(payment, idempotencyKey)` makes a repeated create return the first payment. Mailgun and SendGrid take no key: never retry a send that may have reached the provider. A retryable Billit flow calls `Create`, keeps the id and then `Send(id)`, since repeating `Send(invoice)` creates a second order.
- The handler's 10 s attempt and 30 s total timeouts cut off long conversions: raise `AttemptTimeout` and `TotalRequestTimeout` for Gotenberg and Office.Clients.

## Related Files

- **Consumer bootstrap** — `get_bootstrap_guide(heading: "toc")` — canonical downstream bootstrap; copied to `.regira/bootstrap.md` when the MCP server is not configured, since `./AGENTS.md` is the app's own file
- **Project setup guide** — `get_package(id: "Regira.Setup", section: "project.setup")` — canonical shared project-template guide; extractable to `.regira/instructions/project.setup.md` through `Regira.Setup`

## Authoring Rules

When adding new instruction files:

1. Put shared setup here if more than one module needs it.
2. Keep module guides focused on routing, decision rules, pitfalls, and package-specific behavior.
3. Put examples, setup walkthroughs, and exact signatures in separate deep-reference files.
4. Do not duplicate the module catalog or project-template tables here.
