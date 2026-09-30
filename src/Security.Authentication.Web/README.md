# Regira.Security.Authentication.Web

Ready-made account, password and user endpoints for [Regira Security](https://regira.github.io/Regira-Packages/src/Common.Security/). `AccountControllerBase<TUser>`, `PasswordControllerBase<TUser>` and `UserControllerBase<TUser>` are abstract controllers over ASP.NET Core Identity's `UserManager<TUser>`, subclassed with the application's user type; `IdentityMailer` sends the recover and confirm emails as an `IEmailSender` over [Regira Office mail](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/). For .NET 10 applications the package also ships OpenAPI document transformers that describe the registered authentication schemes.

## Installation

```xml
<PackageReference Include="Regira.Security.Authentication.Web" Version="6.*" />
```

The host application registers ASP.NET Core Identity — a `UserManager<TUser>` with a user store and the default token providers — and the JWT scheme from Regira.Security.Authentication, which this package depends on.

## Documentation

- [Pre-built Auth Controllers](https://regira.github.io/Regira-Packages/src/Common.Security/docs/controllers.html) — required services, routes, the authorization defaults and the OpenAPI transformers
- [Practical Examples](https://regira.github.io/Regira-Packages/src/Common.Security/docs/examples.html) — an account controller, and a user controller with email confirmation

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
