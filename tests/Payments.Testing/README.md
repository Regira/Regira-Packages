# Payments.Testing

Tests for [Payments.Mollie](../../src/Payments.Mollie/README.md): creating payments (with and without metadata),
listing, filtering by amount and reading details against the Mollie API. NUnit.

## Running

```bash
dotnet test tests/Payments.Testing
```

`MollieTests`, the only fixture, is in the `Network` category and needs the user secret `Payments:Mollie:Key`; the
`Payments:Mollie:Api` it also reads fills `MollieConfig.Api`, which the service does not use. There is no skip guard: without the key the fixture fails. The tests create real payments,
so use a Mollie test-mode key. Skip the suite with `--filter "TestCategory!=Network"`.
