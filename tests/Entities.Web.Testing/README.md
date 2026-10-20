# Entities.Web.Testing

xUnit tests for [Entities.Web](../../src/Entities.Web/README.md): entity and attachment controllers, the mapped entity
endpoints (the course tests run against both, the second time on the test API's `Endpoints` surface), minimal APIs,
DTO shapes, JSON options, the exception filter and validation responses, concurrency tokens and archived-entity
contracts. The HTTP tests host [Entities.TestApi](../Entities.TestApi/README.md) through
`WebApplicationFactory<Program>`; the rest build their own service collections.

## Running

```bash
dotnet test tests/Entities.Web.Testing
```

No external requirements. Each HTTP test class gets its own API host with its own SQLite database file and
attachments folder in the temp directory; the database is recreated for every test and both are deleted when the
class finishes. Test classes run in parallel (xUnit's default).
