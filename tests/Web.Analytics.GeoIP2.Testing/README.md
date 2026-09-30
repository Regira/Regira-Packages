# Web.Analytics.GeoIP2.Testing

Tests for [Web.Analytics.GeoIP2](../../src/Web.Analytics.GeoIP2/README.md): registration and configuration,
database path resolution, staying disabled when the database is missing or invalid, and the page-view enricher
against a stub location service. NUnit.

## Running

```bash
dotnet test tests/Web.Analytics.GeoIP2.Testing
```

No external requirements; no GeoLite2 database is needed.
