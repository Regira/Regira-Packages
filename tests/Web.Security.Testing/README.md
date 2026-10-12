# Web.Security.Testing

Tests for [Security.Authentication](../../src/Security.Authentication/README.md) and
[Security.Authentication.Web](../../src/Security.Authentication.Web/README.md): JWT, API key, cookie, OpenID Connect
and Entra ID bearer authentication, refresh tokens, claim normalization, scheme selection, the Identity controllers
and OpenAPI security schemes. xUnit with Shouldly.

## Running

```bash
dotnet test tests/Web.Security.Testing
```

It runs on `net8.0` and `net10.0`, the frameworks the packages ship for, so the .NET 8 runtime must be installed;
`-f net10.0` runs one framework. The OpenAPI document tests run on `net10.0` only: `AddOpenApi` and the package's
document transformers are not built for .NET 8.

The project builds in Debug only: it reads internals of `Security.Authentication`, whose `InternalsVisibleTo` is
granted in Debug builds alone, so `-c Release` fails with CS0122.

Nothing outside the process is needed. Each scenario hosts its own startup through `WebApplicationFactory` on a `TestServer`,
with the EF Core InMemory provider behind Identity and a fake Entra authority, so nothing leaves the process. Test
classes run in parallel under xUnit's default; the project has no `Properties/AssemblyInfo.cs`.
