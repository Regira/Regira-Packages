# Invoicing.Testing

Tests for [Invoicing.ViaAdValvas](../../src/Invoicing.ViaAdValvas/README.md): `PeppolServiceTests` runs `PeppolService` against a
stubbed `HttpMessageHandler` — the address, headers and body of a sent document, and a refusal. NUnit.

## Running

```bash
dotnet test tests/Invoicing.Testing
```

Needs nothing external.
