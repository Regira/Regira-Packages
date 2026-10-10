# Regira Office.Mail

Regira Office.Mail provides a **unified abstraction** for sending email through multiple providers. All implementations share the same `IMailService` interface, making mail backends interchangeable in consuming code.

## Projects

| Project | Package | Backend |
|---------|---------|---------|
| `Common.Office` | *(transitive)* | Shared abstractions, models, and `DummyMailer` |
| `Mail.SendGrid` | `Regira.Office.Mail.SendGrid` | SendGrid API |
| `Mail.MailGun` | `Regira.Office.Mail.MailGun` | Mailgun REST API |
| `Mail.MailKit` | `Regira.Office.Mail.MailKit` | Any SMTP server, through MailKit |
| `Mail.Web` | `Regira.Office.Mail.Web` | HTTP request DTOs for mail endpoints |
| `Mail.MSGReader` | `Regira.Office.Mail.MSGReader` | Read existing `.msg` and `.eml` files |
| `Security.Authentication.Web` | `Regira.Security.Authentication.Web` | `IdentityMailer` bridge to ASP.NET Identity's `IEmailSender` |

## Installation

```xml
<!-- SendGrid -->
<PackageReference Include="Regira.Office.Mail.SendGrid" Version="6.*" />

<!-- Mailgun -->
<PackageReference Include="Regira.Office.Mail.MailGun" Version="6.*" />

<!-- MailKit (SMTP) -->
<PackageReference Include="Regira.Office.Mail.MailKit" Version="6.*" />

<!-- Mail.Web -->
<PackageReference Include="Regira.Office.Mail.Web" Version="6.*" />

<!-- Mail.MSGReader -->
<PackageReference Include="Regira.Office.Mail.MSGReader" Version="6.*" />

<!-- IdentityMailer (ASP.NET Identity integration) -->
<PackageReference Include="Regira.Security.Authentication.Web" Version="6.*" />
```

## Quick Start

```csharp
IServiceCollection services  = new ServiceCollection();
IConfiguration configuration = new ConfigurationBuilder().Build();

// Register (pick one)
services.AddSendGrid(cfg => cfg.Key = configuration["Mail:SendGrid:Key"]!);
// or
services.AddMailGun(cfg =>
{
    cfg.Api    = configuration["Mail:MailGun:Api"]!;
    cfg.Key    = configuration["Mail:MailGun:Key"]!;
    cfg.Domain = configuration["Mail:MailGun:Domain"]!;
});
// or
services.AddMailKit(cfg =>
{
    cfg.Host     = configuration["Mail:Smtp:Host"]!;
    cfg.UserName = configuration["Mail:Smtp:UserName"];
    cfg.Password = configuration["Mail:Smtp:Password"];
});

// Use — the parameters are interface-typed, so construct the concrete models
// (the implicit string conversions don't apply to IMailAddress/IMailRecipient)
IMailService mailer = services.BuildServiceProvider().GetRequiredService<IMailService>();
await mailer.Send(
    sender:     new MailAddress { Email = "no-reply@example.com" },
    recipients: [new MailRecipient { Email = "alice@example.com" }],
    subject:    "Hello",
    message:    "<p>Hi!</p>"
);
```

## IMailService

Every sending backend implements this interface.

<!-- no-compile -->
```csharp
// Parameter-based overload
Task<IMailResponse> Send(
    IMailAddress             sender,
    IEnumerable<IMailRecipient> recipients,
    string?                  subject,
    string?                  message,
    bool                     isHtml      = true,
    IEnumerable<INamedFile>? attachments = null,
    CancellationToken        cancellationToken = default);

// Full message overload
Task<IMailResponse> Send(IMessageObject message, CancellationToken cancellationToken = default);
```

### IMailResponse

| Property | Type | Description |
|----------|------|-------------|
| `Success` | `bool` | `true` when the provider accepted the message |
| `Status` | `string?` | HTTP status code or provider status text; `OK` for SMTP |
| `Content` | `string?` | Raw response body; for SMTP the server's reply, with its queue id (`2.0.0 Ok: queued as …`) |
| `Exception` | `Exception?` | Set when sending fails |

## Core Models

### IMessageObject / MessageObject

Represents a complete outgoing email.

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `From` | `MailAddress?` | `null` | Sender address |
| `To` | `ICollection<MailRecipient>` | `[]` | Recipients (To / Cc / Bcc) |
| `ReplyTo` | `IMailAddress?` | `null` | Reply-To address |
| `Subject` | `string?` | `null` | Email subject |
| `Body` | `string?` | `null` | Message body |
| `IsHtml` | `bool` | `true` | HTML vs plain text |
| `Attachments` | `ICollection<BinaryFileItem>?` | `null` | File attachments |

`MessageObject` exposes the concrete model types shown above; the interface-typed members of
`IMessageObject` (`IMailAddress? From`, `ICollection<IMailRecipient> To`, `ICollection<INamedFile>? Attachments`)
are implemented explicitly and convert to/from the concrete types.

### IMailAddress / MailAddress

| Property | Type | Description |
|----------|------|-------------|
| `Email` | `string` | Email address — validated on assignment |
| `DisplayName` | `string?` | Optional display name |

`MailAddress` supports implicit conversion from `string`:

```csharp
MailAddress addr  = "alice@example.com";
MailAddress named = new() { Email = "alice@example.com", DisplayName = "Alice" };
```

`ToString()` returns `"Alice <alice@example.com>"` when `DisplayName` is set, or just the email.

### IMailRecipient / MailRecipient

Extends `IMailAddress` with a recipient type.

```csharp
public enum RecipientTypes { To, Cc, Bcc }
```

```csharp
MailRecipient to  = "alice@example.com";   // implicit — defaults to RecipientTypes.To
var cc = new MailRecipient { Email = "bob@example.com",   RecipientType = RecipientTypes.Cc };
var bcc = new MailRecipient { Email = "carol@example.com", RecipientType = RecipientTypes.Bcc };
```

## Configuration

### SendGridConfig

| Property | Type | Description |
|----------|------|-------------|
| `Key` | `string` | SendGrid API key |

### MailgunConfig

| Property | Type | Description |
|----------|------|-------------|
| `Api` | `string` | Mailgun API endpoint (e.g. `https://api.mailgun.net/v3`) |
| `Key` | `string` | Mailgun API key |
| `Domain` | `string` | Sending domain |
| `TestMode` | `bool` | Sends with Mailgun's `o:testmode` flag. Default `false`. |

With `TestMode` enabled, Mailgun validates, accepts and logs each call exactly as it would a real one, but
never delivers it to the recipient. The response is a normal success, so code and tests that check
`response.Success` are unaffected. Note that it suppresses **delivery, not billing** — message counts and
charges may still apply.

### MailKitConfig

| Property | Type | Default | Description |
|----------|------|---------|-------------|
| `Host` | `string` | — | SMTP server host name |
| `Port` | `int` | `587` | SMTP port |
| `Security` | `SecureSocketOptions` | `StartTls` | MailKit's TLS mode (`MailKit.Security`); binds from configuration by name |
| `UserName` | `string?` | `null` | SMTP AUTH user — leave empty for a server that takes mail without AUTH |
| `Password` | `string?` | `null` | SMTP AUTH password |
| `Timeout` | `int` | `120000` | Per SMTP command, in milliseconds |

| Server | `Port` | `Security` |
|--------|--------|------------|
| Submission with STARTTLS — Microsoft 365, Gmail (app password), Amazon SES, most providers | `587` | `StartTls` |
| Implicit TLS | `465` | `SslOnConnect` |
| Local test server without TLS — Mailpit, smtp4dev | `1025` / `25` | `None` |

The `StartTls` default fails the send when a server offers no TLS. MailKit's `Auto` would fall back to an
unencrypted connection and send the password in clear text, so keep `StartTls` or `SslOnConnect` for any remote
server. Each send opens its own connection — connect, authenticate, send, quit — which suits transactional
mail; a bulk send pays one TCP, TLS and AUTH handshake per message. OAuth2 sign-in is not supported.

## DI Registration

```csharp
IServiceCollection services = new ServiceCollection();

// SendGrid
services.AddSendGrid(cfg => cfg.Key = "SG.xxx");

// Mailgun
services.AddMailGun(cfg =>
{
    cfg.Api    = "https://api.mailgun.net/v3";
    cfg.Key    = "key-xxx";
    cfg.Domain = "mail.example.com";
});

// Mailgun, accepted and logged but never delivered — for staging hosts and test suites
// that send to real addresses
services.AddMailGun(cfg =>
{
    cfg.Api      = "https://api.mailgun.net/v3";
    cfg.Key      = "key-xxx";
    cfg.Domain   = "mail.example.com";
    cfg.TestMode = true;
});

// MailKit — SMTP submission on port 587 with STARTTLS
services.AddMailKit(cfg =>
{
    cfg.Host     = "smtp.example.com";
    cfg.UserName = "mailer@example.com";
    cfg.Password = "app-password";
});

// MailKit — implicit TLS on port 465
services.AddMailKit(cfg =>
{
    cfg.Host     = "smtp.example.com";
    cfg.Port     = 465;
    cfg.Security = SecureSocketOptions.SslOnConnect;
    cfg.UserName = "mailer@example.com";
    cfg.Password = "app-password";
});

// MailKit — a local Mailpit without TLS or AUTH, for development
services.AddMailKit(cfg =>
{
    cfg.Host     = "localhost";
    cfg.Port     = 1025;
    cfg.Security = SecureSocketOptions.None;
});
```

Each extension method registers `IMailService` as a transient service.

## Exceptions

### MailException

Thrown by the shared `MailerBase` for invalid attachments (missing file name or empty content), and by the
**SendGrid** and **Mailgun** backends when the provider returns a non-success response, an unauthorized
one included. The provider's own error body is on `ResponseContent` — the status code alone rarely says why
a send was refused.

The **MailKit** backend throws it when the SMTP server refuses the credentials, the sender, a recipient or the
message, with the server's reply on `ResponseContent`, and before connecting when the message has no sender or no
recipients. One refused recipient aborts the whole message: nothing is delivered, not even to the accepted
recipients, and `ResponseContent` names the refused address. A connection fault is not a refusal and passes
through unwrapped — `SocketException`, `SslHandshakeException`, `NotSupportedException` (`StartTls` against a
server without STARTTLS), `TimeoutException` or `OperationCanceledException`.

| Property | Type | Description |
|----------|------|-------------|
| `MessageObject` | `IMessageObject?` | The message that failed to send |
| `ResponseContent` | `string?` | Raw provider response body |

### EmailFormatException

Thrown when an invalid email address is assigned to `MailAddress.Email`.

| Property | Type | Description |
|----------|------|-------------|
| `EmailInput` | `string?` | The invalid value that was provided |

## Testing — DummyMailer

`DummyMailer` implements `IMailService` (via `MailerBase`) and sends nothing — it returns an empty `MailResponse`, so `Success` is `false`. Register it in tests to suppress actual sending:

```csharp
IServiceCollection services = new ServiceCollection();
services.AddSingleton<IMailService, DummyMailer>();
```

## Web DTOs — MailInput

`Mail.Web` ships `MailInput` for accepting email requests over HTTP. `MailInputExtensions.ToMessageObject()` converts it to a domain `IMessageObject`.

<!-- no-compile -->
```csharp
[HttpPost]
public async Task<IActionResult> Send([FromBody] MailInput input, IMailService mailer)
{
    var message = input.ToMessageObject();
    var result  = await mailer.Send(message);
    return result.Success ? Ok() : StatusCode(502);
}
```

### MailInput structure

| Property | Type | Validation | Description |
|----------|------|------------|-------------|
| `From` | `Address?` | — | Optional sender override |
| `To` | `ICollection<Recipient>?` | `[Required]` | Recipients |
| `ReplyTo` | `Address?` | — | Reply-To address |
| `Subject` | `string?` | `[Required]` | Email subject |
| `Body` | `string?` | — | HTML or plain text body |
| `IsHtml` | `bool` | — | Defaults to `true` |
| `Attachments` | `ICollection<Attachment>?` | — | File attachments |

`Address` and `Recipient` both support implicit conversion from a plain email string.

## ASP.NET Identity Integration

`IdentityMailer` (from the `Regira.Security.Authentication.Web` package, namespace
`Regira.Security.Authentication.Web.Mail`) bridges `IMailService` to the ASP.NET Identity `IEmailSender` interface.

```csharp
IServiceCollection services = new ServiceCollection();
services.AddSingleton<IEmailSender>(provider =>
    new IdentityMailer(
        provider.GetRequiredService<IMailService>(),
        new IdentityMailerOptions { Sender = "no-reply@example.com" }
    ));
```

## Overview

1. **[Index](README.md)** — Overview, interface, models, and configuration reference
1. [Examples](examples.md) — Simple send, attachments & multiple recipients, backend swap, Identity integration
