# Web.Security.Testing

Tests for [Security.Authentication](../../src/Security.Authentication/README.md) and
[Security.Authentication.Web](../../src/Security.Authentication.Web/README.md): JWT, API key, cookie, OpenID Connect
and Entra ID bearer authentication, refresh tokens, claim normalization, scheme selection, the Identity controllers
and OpenAPI security schemes. xUnit with Shouldly.

## Running

```bash
dotnet test tests/Web.Security.Testing
```

No external requirements. Each scenario hosts its own startup through `WebApplicationFactory` on a `TestServer`,
with the EF Core InMemory provider behind Identity and a fake Entra authority, so nothing leaves the process. Test
classes run in parallel under xUnit's default; the project has no `Properties/AssemblyInfo.cs`.
