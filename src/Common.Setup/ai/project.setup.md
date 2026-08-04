# Project Setup for Regira AI Instructions

> **Role:** Load this file only when the task creates a new project or changes project shape, hosting, logging, authentication, OpenAPI, or baseline DI structure.
>
> **Boundaries:** Do not use this file for module discovery or package-family routing; use [`AGENTS.md`](../../../ai/AGENTS.md) for that. For cross-module setup rules reused by multiple guides, use [`shared.setup.md`](./shared.setup.md).
>
> **NuGet package versions:** Never guess a specific version number — omit it and let NuGet resolve the **latest stable**, then pin what restored (the numbers in this document are illustrative, not authoritative). Where a package guide ships a known-good list (e.g. `Regira.Entities` → `entities.setup`), start from it so you don't float from `*`.

This file owns the reusable starter templates. Choose the matching template first, then apply the setup baseline below.

---

## Template Selection Guide

| Requirement | Template |
|---|---|
| Script, batch job, or CLI utility | `ConsoleWithLogging` |
| Standard hosted API, Minimal API and Controllers, no auth | `BasicApi` |
| Lightweight self-hosted internal API, no auth | `SelfHostingApi` |
| API protected by API key and/or JWT Bearer | `SelfHostingApiWithAuth` |
| Must be deployable as a Windows Service | `SelfHostingApi` |
| Controller-based routing with enforced authorization | `SelfHostingApiWithAuth` |
| Minimal API endpoints with authentication | `SelfHostingApiWithAuth` |
| Users sign in with a work Microsoft account (Entra ID), or any OpenID Connect provider | `SelfHostingApiWithAuth` + `AddEntraIdSignIn` / `AddOidcAuthentication` |
| API called with tokens Entra ID (or Auth0 / Keycloak / Okta) issued | `SelfHostingApiWithAuth` + `AddEntraIdBearer` / `AddBearerAuthentication` |
| Browser session rather than a bearer token (server-rendered, Blazor Server, same-site SPA) | `SelfHostingApiWithAuth` + `AddCookieAuthentication` |

`SelfHostingApiWithAuth` is the scaffold for **any** authenticated app — the scheme is a registration choice on top
of it, not a different template. See *Authentication conventions* → *Picking a scheme* below.

---

## Shared Conventions

These rules apply to all templates.

### Framework & language

- **TFM:** `net10.0` — do not change unless explicitly asked.
- **Solution file:** on .NET 10, `dotnet new sln` emits the XML **`.slnx`** format — target `MyProject.slnx` in `dotnet build` / `dotnet sln` commands; do not assume a `.sln` exists.
- **C# 14** — use modern features (primary constructors, collection expressions `[..]`, raw string literals `"""`, file-scoped namespaces) where appropriate.
- `<ImplicitUsings>enable</ImplicitUsings>` and `<Nullable>enable</Nullable>` are always on.
- Replace `MyProject` with the actual project name throughout all files.

### Basic Project file

**Console App**
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

**API**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">
  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
</Project>
```

### Default File structure

```
MyProject/
├── Properties/
│   └── launchSettings.json // API only
├── Infrastructure/
│   └── HostingExtensions.cs   # See Shared Conventions
├── Program.cs
├── appsettings.json
```

### Logging (serilog)

All templates use Serilog with console + rolling file sinks configured from `appsettings.json`.

**Packages**

```xml
<PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="*" />
    <PackageReference Include="Microsoft.Extensions.Hosting" Version="*" />
    <PackageReference Include="Serilog.Extensions.Hosting" Version="*" />
    <PackageReference Include="Serilog.Settings.Configuration" Version="*" />
    <PackageReference Include="Serilog.Sinks.Console" Version="*" />
    <PackageReference Include="Serilog.Sinks.File" Version="*" />
```

**Packages — Web APIs **

```xml
<PackageReference Include="Serilog.AspNetCore" Version="*" />
```

**Bootstrap pattern (web APIs)**

Wrap the entire `Program.cs` body in a bootstrap logger + try/catch/finally:

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host
        .UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

    // ... rest of setup ...

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
```

**`appsettings.json` Serilog block**

Used by all templates.

```json
"Serilog": {
  "Using": [ "Serilog.Sinks.Console", "Serilog.Sinks.File" ],
  "MinimumLevel": {
    "Default": "Information",
    "Override": {
      "Microsoft.AspNetCore": "Warning",
      "Microsoft.EntityFrameworkCore": "Warning",
      "System": "Warning"
    }
  },
  "WriteTo": [
    { "Name": "Console" },
    {
      "Name": "File",
      "Args": {
        "path": "logs/MyProject-.log",
        "restrictedToMinimumLevel": "Information",
        "rollingInterval": "Month",
        "rollOnFileSizeLimit": true,
        "retainedFileCountLimit": 12
      }
    }
  ],
  "Enrich": [ "FromLogContext" ]
}
```

### OpenAPI & Scalar UI

All API templates expose:
- **OpenAPI JSON** `/openapi/v1.json` — `app.MapOpenApi()`
- **Scalar UI** `/scalar` — `app.MapScalarApiReference()`

Use this as the default API documentation surface for Regira projects. Do not add `Swashbuckle.AspNetCore` and do not call `app.UseSwaggerUI()` unless the user explicitly asks to deviate from the standard template.

⚠️ **Adding authentication to any template? The plain `AddOpenApi()` / `MapScalarApiReference()` pair above is then incomplete** — Scalar renders every endpoint but offers no way to authenticate, so `/scalar` cannot exercise a guarded API. This applies to Templates 2–4 alike; it is not specific to `SelfHostingApiWithAuth`. Declare the schemes, and mark the operations that require one — a declared scheme only produces the auth prompt; without the operation transformer the document says nothing about which endpoints are guarded, and a generated client cannot tell:

```csharp
using Regira.Security.Authentication.Web.OpenApi.Transformers;

builder.Services.AddOpenApi(options =>
{
    // declares every registered scheme — API key, JWT, cookie, OpenID Connect — from the descriptor
    // each Add…Authentication contributes, so adding a scheme needs no change here
    options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
    // declares WHICH operations need one — without it every endpoint reads as anonymous
    options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
});

app.MapOpenApi().AllowAnonymous();
app.MapScalarApiReference(options =>
{
    options.Authentication = new ScalarAuthenticationOptions
    {
        PreferredSecuritySchemes = [ApiKeyDefaults.AuthenticationScheme, JwtBearerDefaults.AuthenticationScheme]
    };
});
```

**Nuget packages — Web API**
```xml
<PackageReference Include="Microsoft.AspNetCore.OpenApi" Version="*" />
<PackageReference Include="Scalar.AspNetCore" Version="*" />
```

> **⚠️ On the native `Microsoft.AspNetCore.OpenApi`/Scalar path, `Microsoft.OpenApi` must stay on 2.x** _(while on .NET 10 — remove once the OpenAPI source generator supports 3.x)_. It comes in transitively via `Microsoft.AspNetCore.OpenApi`. (This does **not** apply to a Swashbuckle setup, which supports 3.x.)
> When pinning it directly (e.g. to clear the security advisory on 2.0.0), add the reference **by hand**:
> `<PackageReference Include="Microsoft.OpenApi" Version="2.9.*" />`. Never `dotnet add package
> Microsoft.OpenApi` — that resolves the latest 3.x, which breaks the .NET 10 OpenAPI source generator.

### Launch API

All API templates include this launch profile, opening the Scalar UI on start.

**Properties/launchSettings.json**
```json
{
  "profiles": {
    "https": {
      "commandName": "Project",
      "dotnetRunMessages": true,
      "launchBrowser": true,
      "launchUrl": "scalar",
      "applicationUrl": "<URLS>",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

### Extension methods

- Services and middleware registration are placed in extension methods, never inline in `Program.cs`.
  - Console/Worker: `IHostBuilder` extensions in `Infrastructure/HostExtensions.cs`
  - Web APIs: per-concern static extension classes (e.g. `Infrastructure/HostExtensions.cs` or `Infrastructure/EndpointExtensions.cs`)
- `Program.cs` stays as a thin orchestrator: build → configure → run.

### Console configuration

Enrich console apps with appsettings, user secrets and environment variables.
(Not applicable to APIs since they implement user secrets by default.)

**.csproj file — Console App**
```xml
  <!-- NuGet package -->
  <PackageReference Include="Microsoft.Extensions.Configuration.UserSecrets" Version="*" />

  <!-- include appsettings.json in output for console apps -->
  <ItemGroup>
    <Content Include="appsettings.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
```

**HostExtensions**
```csharp
public static IHostBuilder AddConfiguration(this IHostBuilder builder)
{
    return builder.ConfigureAppConfiguration((_, config) =>
    {
        config.Sources.Clear();
        // add configuration
        config
            .AddEnvironmentVariables()
            .AddJsonFile("appsettings.json", true, true)
#if DEBUG
            .AddUserSecrets(typeof(Program).Assembly, true)
#endif
            ;
    });
}
```

### Windows Service support

- `WindowsServiceHelpers.IsWindowsService()` guards `UseWindowsService()` so the app runs normally outside the service host.
- `AddWindowsServiceInstaller` generates `install.bat` / `uninstall.bat` on first run (idempotent).
- `AddWindowsServiceInstaller` is provided by `Regira.System.Hosting` (`WindowsServiceHostExtensions`).

### Authentication conventions

- API keys are passed in the `X-Api-Key` request header.
- JWT tokens use the `Authorization: Bearer <token>` header.
- **`.AddSchemeSelector()` forwards each request to the scheme matching its credential** — bearer header → JWT,
  API-key header → API key, auth cookie → cookie. ⚠️ **Call it last**: every `Add…Authentication` sets its own
  default scheme, so without it the registration order silently decides which handler an unattributed
  `[Authorize]` uses, and the symptom is a 401 for a caller holding a perfectly good credential of the other kind.
  It also removes the need for an `AddAuthorization` default policy naming the schemes.
- Authorization is **enforced globally** via `.RequireAuthorization()` on `MapControllers()`.
- OpenAPI / Scalar endpoints are explicitly marked `.AllowAnonymous()`.
- Controllers that must be public use `[AllowAnonymous]` per action.

**Picking a scheme**

| The app needs | Use |
|---|---|
| A SPA or mobile client against your own user table | `AddJwtAuthentication` (+ `AddRefreshTokens` so sessions survive token expiry) |
| Machine-to-machine callers | `AddApiKeyAuthentication` |
| A server-rendered app, Blazor Server, or same-site SPA | `AddCookieAuthentication` |
| Staff signing in with their work Microsoft account | `AddEntraIdSignIn` (browser) |
| An API called with tokens Entra issued | `AddEntraIdBearer` |
| Any other OpenID Connect provider (Auth0, Keycloak, Okta) | `AddBearerAuthentication` with its `Authority` |
| More than one of the above | all of them, then `AddSchemeSelector()` **last** |

Cookie and OIDC gotchas that bite in deployment rather than development: the auth cookie is `Secure` by default so
it is never sent over plain HTTP (sign-in appears to work and every later request 401s); a multi-instance host needs
a shared, persisted Data Protection key ring plus `SetApplicationName` or restarts log everyone out; and behind a
reverse proxy the OIDC callback fails with "Correlation failed" unless `UseForwardedHeaders` runs before the
authentication middleware.

**Nuget packages**
```xml
<PackageReference Include="Regira.Security.Authentication" Version="*" />
<PackageReference Include="Regira.Security.Authentication.Web" Version="*" />
```

> `Regira.Security.Authentication.Web` references `Microsoft.AspNetCore.OpenApi` and thereby **floors** it
> (and, through it, `Microsoft.OpenApi`). Pinning either lower than the floor fails restore with
> **NU1605 (package downgrade)** — resolve them to the latest stable patch instead of an older pin.

**appsettings.json — Authentication block**
```json
{
  "Authentication": {
    "ApiKeys": [
      { "OwnerId": "MyUser", "Key": "REPLACE-WITH-GUID", "Roles": ["read"] }
    ],
    "Jwt": {
      "Secret": "REPLACE-WITH-RANDOM-SECRET-OF-AT-LEAST-64-CHARS-TO-FIT-HS512-SIGNING-KEYS!!",
      "Audience": "my-spa",
      "LifeSpan": 7200
    }
  }
}
```

Section names are constants on `AuthenticationSections`: `Jwt`, `Bearer`, `ApiKeys`, `Cookie`, `Oidc`, `EntraId`.
Only add the blocks the app registers a scheme for:

```json
{
  "Authentication": {
    "Cookie": {
      "IsApi": true,
      "CookieName": ".Regira.Auth",
      "ExpireTimeSpan": "08:00:00"
    },
    "EntraId": {
      "TenantId": "REPLACE-WITH-DIRECTORY-TENANT-ID",
      "ClientId": "REPLACE-WITH-APPLICATION-CLIENT-ID",
      "ClientSecret": "REPLACE-WITH-CLIENT-SECRET-ONLY-FOR-INTERACTIVE-SIGN-IN"
    },
    "Bearer": {
      "Authority": "https://your-tenant.eu.auth0.com/",
      "Audience": "https://api.example.com"
    }
  }
}
```

`ClientSecret` is required for interactive sign-in (`AddEntraIdSignIn`) and unused for API protection
(`AddEntraIdBearer`). Keep it out of `appsettings.json` — use user-secrets or a key vault.

> **⚠️ JWT secret length must match the signing algorithm.** The default algorithm is **HS512**, which
> requires a key of **≥ 64 bytes (512 bits)**; a shorter secret throws from `AddJwtAuthentication` at
> startup, naming the byte count it got and the one it needs. Generate ≥ 64 random characters (HS384 would
> need ≥ 48, HS256 ≥ 32). Preflight the other runtime-only setting with it:
> `Jwt:Audience` must equal the SPA's `clientApp`. `LifeSpan` is an **`int` in seconds** (default 7200) —
> a `TimeSpan` string fails config binding at startup, and `60` means one minute, not one hour.

**Key auth model types**

Provided by `Regira.Security.Authentication`:

- **`ApiKeyOwner`** — `{ OwnerId, Key, Roles, Claims }` — registered caller identity
- **`ApiKeyDefaults`** — `AuthenticationScheme` + `HeaderName` constants
- **`JwtTokenOptions`** — `{ Secret, Algorithm, Authority, Audience, LifeSpan, ValidateSecretLength, ... }`
- **`AuthenticationSections`** — the `Authentication:*` configuration paths
- **`SchemeSelectorOptions`** / **`SchemeForwardRules`** — multi-scheme forwarding
- **`CookieAuthOptions`** / **`CookieAuthDefaults`** — cookie sessions (note: **not** `CookieAuthenticationDefaults`,
  which is the framework's own type)
- **`BearerValidationOptions`** — validating an external authority's tokens
- **`EntraIdOptions`** (API) / **`EntraIdSignInOptions`** (browser) / **`EntraIdDefaults`**
- **`OidcAuthOptions`** — interactive OpenID Connect sign-in
- **`RefreshTokenOptions`** / **`TokenPair`** / **`RefreshTokenRecord`** — refresh tokens
- **`RegiraClaimTypes`** / **`ClaimNormalizationOptions`** — the canonical claim spellings every scheme emits

### Self hosting APIs

- Kestrel is always configured from `appsettings.json` via `options.Configure(context.Configuration.GetSection("Kestrel"))`.

**Nuget packages**
```xml
<PackageReference Include="Regira.System.Hosting" Version="*" />
```

**appsettings.json — Kestrel block**
```json
{
  "Kestrel": {
    "Endpoints": {
      "Http": {
        "Url": "http://localhost:9001"
      },
      "Https": {
        "Url": "https://localhost:9002",
        "Certificate": {
          "Path": "xxx.pfx", // Better include in secrets or safer sources
          "Password": "XXX", // Better include in secrets or safer sources
        }
      }
    }
  }
}
```

- User secrets are only added in `#if DEBUG` blocks.

### Self-signed certificate (`your-certificate.pfx`)

Templates 3 and 4 require a certificate for local HTTPS. Generate one with OpenSSL:

```sh
openssl req -x509 -newkey rsa:4096 -keyout key.pem -out cert.pem -days 365
openssl pkcs12 -export -out your-certificate.pfx -inkey key.pem -in cert.pem
```

---

## Template 1 — `ConsoleWithLogging`

### Use when
Standalone console application for a task, script, or batch job — with structured logging, dependency injection, and configuration support.

### Project file

```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <OutputType>Exe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>enable</Nullable>
  </PropertyGroup>
  <ItemGroup>
    <Content Include="appsettings.json">
      <CopyToOutputDirectory>Always</CopyToOutputDirectory>
    </Content>
  </ItemGroup>
</Project>
```

### `Program.cs`

```csharp
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using MyProject.Infrastructure;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = Host.CreateDefaultBuilder(args);
    var host = builder
        .AddSerilog()
        .AddConfiguration()
        .AddServices()
        .Build();

    using var scope = host.Services.CreateScope();
    var logger = scope.ServiceProvider.GetRequiredService<ILogger<Program>>();
    logger.LogInformation("Start");
    Console.WriteLine();

    // Execute code here

    Console.WriteLine();
    logger.LogInformation("Finished");
}
catch (Exception ex)
{
    Log.Error(ex, "Host failed");
}
finally
{
    Console.WriteLine("Press enter to exit");
    Console.ReadLine();
    Log.CloseAndFlush();
}
```

### `Infrastructure/HostingExtensions.cs`

```csharp
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Serilog;

namespace MyProject.Infrastructure;

public static class HostingExtensions
{
    public static IHostBuilder AddConfiguration(this IHostBuilder builder)
        => builder.ConfigureAppConfiguration((_, config) =>
        {
            config.Sources.Clear();
            config
                .AddEnvironmentVariables()
                .AddJsonFile("appsettings.json", true, true)
#if DEBUG
                .AddUserSecrets(typeof(Program).Assembly, true)
#endif
                ;
        });

    public static IHostBuilder AddServices(this IHostBuilder builder)
        => builder.ConfigureServices((context, services) =>
        {
            var config = context.Configuration;
            // register services here
        });

    public static IHostBuilder AddSerilog(this IHostBuilder builder)
    {
        builder.UseSerilog((context, configuration) =>
            configuration.ReadFrom.Configuration(context.Configuration));
        return builder;
    }
}
```

---

## Template 2 — `BasicApi`

### Use when
Standard ASP.NET Core API hosted on IIS, Azure, or Docker. No authentication. Supports both controller-based routing and Minimal API endpoints.

### `Program.cs`

```csharp
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

    builder.Services.AddControllers()
        .AddJsonOptions(options =>
        {
            options.JsonSerializerOptions.ReferenceHandler = ReferenceHandler.IgnoreCycles; // recommended
            options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull; // recommended
            options.JsonSerializerOptions.Converters.Add(new JsonStringEnumConverter()); // recommended — enums as names, a stable SPA contract
        });
    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.MapOpenApi();
    app.MapScalarApiReference();
    
    app.UseHttpsRedirection();

    // app.UseAuthentication();
    app.UseAuthorization();

    app.MapControllers();
    // app.AddEndpoints(); // enable at wish

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
```

> **Entities + OpenAPI:** the JSON options above configure controllers only. `AddOpenApi()` instead reads
> `Http.Json.JsonOptions`, so on a `Regira.Entities` API swap this block for `ConfigureDefaultJsonOptions()`
> (`Regira.Entities.Web.DependencyInjection`) — it applies cycles/nulls/enum-names to **both** sets so the
> generated schema matches the wire format (`get_package("Regira.Entities", "entities.setup")` → P3).

> A browser SPA on `http://` is 307-redirected by `UseHttpsRedirection()`; see *Calling the API in dev*
> (front-end `regira_modules.vue.entities` → `entities.setup`) to wire the dev SPA to this API.

---

## Template 3 — `SelfHostingApi`

### Use when
Lightweight self-hosted HTTP API, optionally deployable as a Windows Service. No authentication.

### `Program.cs`

```csharp
using Microsoft.Extensions.Hosting.WindowsServices;
using Regira.System.Hosting.WindowsService;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

    if (WindowsServiceHelpers.IsWindowsService())
        builder.Host.UseWindowsService();

    builder.WebHost.ConfigureKestrel((context, options) =>
        options.Configure(context.Configuration.GetSection("Kestrel")));

    builder.Services.AddControllers();

    builder.Services.AddOpenApi();

    var app = builder.Build();

    app.MapOpenApi();
    app.MapScalarApiReference();

    app
        .UseRouting()
        //.UseAuthentication()
        .UseAuthorization()
        .UseEndpoints(endpoints =>
        {
            endpoints.MapControllers();
        });

    var host = app.Services.GetRequiredService<IHost>();
    host.AddWindowsServiceInstaller(new WindowsServiceOptions { ServiceName = "MyProject" }); // Replace with actual service name

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
```
---

## Template 4 — `SelfHostingApiWithAuth`

### Use when
Self-hosted HTTP API with controller-based routing and enforced authorization. The `Program.cs` below wires API key
+ JWT Bearer, which is the common case; cookie sessions, Entra ID and OpenID Connect are registration changes on the
same scaffold — see *Authentication conventions* → *Picking a scheme*. Deployable as a Windows Service.

### `Program.cs`

```csharp
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Hosting.WindowsServices;
using Regira.Security.Authentication.ApiKey.Extensions;
using Regira.Security.Authentication.ApiKey.Models;
using Regira.Security.Authentication.Core.Extensions;
using Regira.Security.Authentication.Jwt.Extensions;
using Regira.Security.Authentication.Web.OpenApi.Transformers;
using Regira.System.Hosting.WindowsService;
using Scalar.AspNetCore;
using Serilog;

Log.Logger = new LoggerConfiguration().WriteTo.Console().CreateLogger();

try
{
    var builder = WebApplication.CreateBuilder(args);
    builder.Host.UseSerilog((ctx, cfg) => cfg.ReadFrom.Configuration(ctx.Configuration));

    if (WindowsServiceHelpers.IsWindowsService())
        builder.Host.UseWindowsService();

    builder.WebHost.ConfigureKestrel((context, options) =>
        options.Configure(context.Configuration.GetSection("Kestrel")));

    builder.Services.AddControllers();

    // Authentication — API Key
    builder.Services
        .AddApiKeyAuthentication()
        .AddInMemoryApiKeyAuthentication(
            builder.Configuration.GetSection("Authentication:ApiKeys").ToApiKeyOwners()
        );

    // Authentication — JWT Bearer, then the scheme selector LAST. It forwards each request to the
    // scheme matching the credential it carries (Authorization: Bearer → JWT, X-Api-Key → API key)
    // and becomes the default authenticate and challenge scheme, so the order the schemes were
    // registered in stops deciding what an unattributed [Authorize] authenticates against.
    builder.Services
        .AddJwtAuthentication(o =>
        {
            o.Secret = builder.Configuration["Authentication:Jwt:Secret"]
                ?? throw new NullReferenceException("Secret is missing");
        })
        .AddSchemeSelector();

    // OpenAPI — two transformers cover every registered scheme: the first declares them from the
    // descriptors each Add…Authentication contributes, the second marks which operations need one
    // (and resolves the selector's policy scheme to the real schemes behind it).
    builder.Services.AddOpenApi(options =>
    {
        options.AddDocumentTransformer<AuthenticationSchemeDocumentTransformer>();
        options.AddOperationTransformer<SecurityRequirementOperationTransformer>();
    });

    var app = builder.Build();

    app.MapOpenApi()
        .AllowAnonymous();
    app.MapScalarApiReference(options =>
    {
        options.Authentication = new ScalarAuthenticationOptions
        {
            PreferredSecuritySchemes = [
                ApiKeyDefaults.AuthenticationScheme,
                JwtBearerDefaults.AuthenticationScheme
            ]
        };
    });

    app
        .UseRouting()
        .UseAuthentication()
        .UseAuthorization()
        .UseEndpoints(endpoints =>
        {
            endpoints.MapControllers()
              .RequireAuthorization();
        });

    var host = app.Services.GetRequiredService<IHost>();
    host.AddWindowsServiceInstaller(new WindowsServiceOptions { ServiceName = "MyProject" }); // Replace with actual service name

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Application start-up failed");
}
finally
{
    Log.CloseAndFlush();
}
```

