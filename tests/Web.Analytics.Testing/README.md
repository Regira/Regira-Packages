# Web.Analytics.Testing

Tests for [Web.Analytics](../../src/Web.Analytics/README.md): the visit-tracking middleware, bot detection, IP
masking, the HTML page filter, page-view writing and retention, custom page-view types, builder registration and the
analytics endpoints. The integration tests run on an in-memory `TestServer` with in-memory stores. NUnit.

## Running

```bash
dotnet test tests/Web.Analytics.Testing
```

No external requirements.
