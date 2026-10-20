# Regira Office.Mail AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Mail` | Email sending, mail DTOs for HTTP endpoints, and `.msg` / `.eml` reading |

Exact usings: `office.namespaces.md`.

**Related:**
- **IO.Storage** — `INamedFile` used for email attachments. `get_package(id: "Regira.IO.Storage", section: "io.storage.instructions")`, or `io.storage.instructions.md` locally.

---

## Installation

```bash
# SendGrid
dotnet add package Regira.Office.Mail.SendGrid

# Mailgun
dotnet add package Regira.Office.Mail.MailGun

# MailKit — any SMTP server
dotnet add package Regira.Office.Mail.MailKit

# Mail.Web — HTTP request DTOs for mail endpoints
dotnet add package Regira.Office.Mail.Web

# MSGReader — read existing .msg and .eml files
dotnet add package Regira.Office.Mail.MSGReader
```

---

## `IMailService`

Every sending backend implements this interface. HTML you build formats dates and numbers in the thread's culture: the host's in a reactor, a job and a request
without request localization. Set `CultureInfo.DefaultThreadCurrentCulture` at startup, or pass a culture to `ToString`.

<!-- no-compile -->
```csharp
// Parameter-based
Task<IMailResponse> Send(
    IMailAddress                sender,
    IEnumerable<IMailRecipient> recipients,
    string?                     subject,
    string?                     message,
    bool                        isHtml      = true,
    IEnumerable<INamedFile>?    attachments = null,
    CancellationToken           cancellationToken = default);

// Full message object
Task<IMailResponse> Send(IMessageObject message, CancellationToken cancellationToken = default);
```

### `IMailResponse`

| Property | Type | Description |
|---|---|---|
| `Success` | `bool` | `true` whenever a sending backend's `Send` returns: SendGrid, Mailgun and MailKit throw `MailException` on a refusal instead (see *Exceptions*). `DummyMailer` returns `false` |
| `Status` | `string?` | HTTP status code or provider status text; `OK` for SMTP |
| `Content` | `string?` | Raw response body; for SMTP the server's reply, with its queue id (`2.0.0 Ok: queued as …`) |
| `Exception` | `Exception?` | Not set by the shipped backends, which throw instead |

---

## Core Models

### `IMessageObject` / `MessageObject`

| Property | Type on `IMessageObject` | Type on `MessageObject` | Default | Description |
|---|---|---|---|---|
| `From` | `IMailAddress?` | `MailAddress?` | `null` | Sender address |
| `To` | `ICollection<IMailRecipient>` | `ICollection<MailRecipient>` | `[]` | Recipients (To / Cc / Bcc) |
| `ReplyTo` | `IMailAddress?` | `IMailAddress?` | `null` | Reply-To address |
| `Subject` | `string?` | `string?` | `null` | Email subject |
| `Body` | `string?` | `string?` | `null` | Message body |
| `IsHtml` | `bool` | `bool` | `true` | HTML vs plain text |
| `Attachments` | `ICollection<INamedFile>?` | `ICollection<BinaryFileItem>?` | `null` | File attachments |

### `IMailAddress` / `MailAddress`

| Property | Type | Description |
|---|---|---|
| `Email` | `string` | Email address — validated on assignment |
| `DisplayName` | `string?` | Optional display name |

```csharp
using Regira.Office.Mail.Models;                        // MailAddress, MailRecipient

MailAddress addr  = "alice@example.com";  // implicit from a bare address only: "Alice <alice@example.com>" throws EmailFormatException; set DisplayName instead
MailAddress named = new() { Email = "alice@example.com", DisplayName = "Alice" };
```

`ToString()` returns `"Alice <alice@example.com>"` when `DisplayName` is set.

The implicit conversions from a string — this one and `MailRecipient`'s below — apply only where the concrete type is
expected. `Send` takes `IMailAddress` and `IEnumerable<IMailRecipient>`, so a string passed to it fails with CS1503:
construct the model, or convert into a typed variable first.

### `IMailRecipient` / `MailRecipient`

Extends `IMailAddress` with a recipient type.

```csharp
using Regira.Office.Mail.Models;                        // MailAddress, MailRecipient, RecipientTypes

MailRecipient to  = "alice@example.com";                                              // defaults to To
var cc  = new MailRecipient { Email = "bob@example.com",   RecipientType = RecipientTypes.Cc };
var bcc = new MailRecipient { Email = "carol@example.com", RecipientType = RecipientTypes.Bcc };
```

---

## Configuration

### `SendGridConfig`

| Property | Type | Description |
|---|---|---|
| `Key` | `string` | SendGrid API key |

### `MailgunConfig`

| Property | Type | Description |
|---|---|---|
| `Api` | `string` | Mailgun API endpoint (e.g. `https://api.mailgun.net/v3`) |
| `Key` | `string` | Mailgun API key |
| `Domain` | `string` | Sending domain |
| `TestMode` | `bool` | Sends with `o:testmode` — Mailgun accepts and logs the call but never delivers. Default `false`. |

> **`TestMode` suppresses delivery, not billing.** Mailgun still processes and logs the message, and message
> counts and charges may still apply. The response is indistinguishable from a real send, so a test asserting
> on `response.Success` keeps working. Use it for suites and staging hosts that send to real addresses.

### `MailKitConfig`

| Property | Type | Default | Description |
|---|---|---|---|
| `Host` | `string` | — | SMTP server host name |
| `Port` | `int` | `587` | SMTP port |
| `Security` | `SecureSocketOptions` (`MailKit.Security`) | `StartTls` | TLS mode; a configuration binder reads it by name (`"SslOnConnect"`) — `services.AddMailKit(cfg => configuration.GetSection("Mail:Smtp").Bind(cfg))` |
| `UserName` | `string?` | `null` | SMTP AUTH user; leave empty for a server that takes mail without AUTH |
| `Password` | `string?` | `null` | SMTP AUTH password |
| `Timeout` | `int` | `120000` | Per SMTP command, in milliseconds |

| Server | `Port` | `Security` |
|---|---|---|
| Submission with STARTTLS — Microsoft 365, Gmail (app password), Amazon SES, most providers | `587` | `StartTls` |
| Implicit TLS | `465` | `SslOnConnect` |
| Local test server without TLS — Mailpit, smtp4dev | `1025` / `25` | `None` |

> **Keep `StartTls` for a remote server.** It fails the send when the server offers no TLS. MailKit's `Auto`
> falls back to an unencrypted connection on such a server and then sends the password in clear text.

Each send opens its own connection — connect, authenticate, send, quit — which suits transactional mail. A
bulk send pays one TCP, TLS and AUTH handshake per message. OAuth2 sign-in (for tenants with basic SMTP AUTH
disabled) is not supported.

---

## DI Registration

<!-- no-compile -->
```csharp
using Regira.Office.Mail.SendGrid.DependencyInjection;   // AddSendGrid
using Regira.Office.Mail.MailGun.DependencyInjection;    // AddMailGun
using Regira.Office.Mail.MailKit.DependencyInjection;    // AddMailKit
// keep the one for the provider you register; inject IMailService (Regira.Office.Mail.Abstractions)

// SendGrid
services.AddSendGrid(cfg => cfg.Key = configuration["Mail:SendGrid:Key"]!);

// Mailgun
services.AddMailGun(cfg =>
{
    cfg.Api    = configuration["Mail:MailGun:Api"]!;
    cfg.Key    = configuration["Mail:MailGun:Key"]!;
    cfg.Domain = configuration["Mail:MailGun:Domain"]!;
    cfg.TestMode = !environment.IsProduction();  // accepted and logged, never delivered
});

// MailKit (SMTP)
services.AddMailKit(cfg =>
{
    cfg.Host     = configuration["Mail:Smtp:Host"]!;
    cfg.Port     = int.Parse(configuration["Mail:Smtp:Port"] ?? "587");
    cfg.UserName = configuration["Mail:Smtp:UserName"];
    cfg.Password = configuration["Mail:Smtp:Password"];
    // a local test server (Mailpit, port 1025) speaks no TLS: cfg.Security = MailKit.Security.SecureSocketOptions.None
});
// or the whole section, Security by name included: services.AddMailKit(cfg => configuration.GetSection("Mail:Smtp").Bind(cfg));
```

Each extension method registers `IMailService` as a transient service that resolves from the root provider too, in a singleton say; its config is built from the configure action on every resolve. SendGrid and Mailgun send through a connection pool their package shares. Neither provider takes an idempotency key, so never retry a send that may have reached it.

A missing key fails only when a mailer is resolved or sends. To fail at start-up instead, validate the config:

<!-- no-compile -->
```csharp
services.AddOptions<Regira.Office.Mail.SendGrid.SendGridConfig>()
    .Validate(c => !string.IsNullOrEmpty(c.Key), "Mail:SendGrid:Key is missing")
    .ValidateOnStart();
```

**Two channels, or a provider chosen by configuration.** Every `Add…` registers the one `IMailService`, so a second
call replaces the first. Each mailer has a public constructor that takes its config: build them as keyed services and
choose by whether a key is set.

```csharp
using Regira.Office.Mail.Abstractions;   // IMailService
using Regira.Office.Mail.MailGun;        // MailGunMailer, MailgunConfig
using Regira.Office.Mail.MailKit;        // MailKitMailer, MailKitConfig
using Regira.Office.Mail.SendGrid;       // SendGridMailer, SendGridConfig

var smtp = configuration.GetSection("Mail:Smtp").Get<MailKitConfig>()!;
services.AddKeyedTransient<IMailService>("transactional", (_, _) => configuration["Mail:SendGrid:Key"] is { Length: > 0 } key
    ? new SendGridMailer(new SendGridConfig { Key = key })
    : new MailKitMailer(smtp));
services.AddKeyedTransient<IMailService>("newsletter", (_, _) => configuration["Mail:MailGun:Key"] is { Length: > 0 } key
    ? new MailGunMailer(new MailgunConfig { Api = configuration["Mail:MailGun:Api"]!, Key = key, Domain = configuration["Mail:MailGun:Domain"]! })
    : new MailKitMailer(smtp));
// the unkeyed IMailService, for consumers such as IdentityMailer
services.AddTransient(sp => sp.GetRequiredKeyedService<IMailService>("transactional"));
// inject a channel with [FromKeyedServices("newsletter")] IMailService
```

---

## Exceptions

### `MailException`

Thrown by `MailerBase` for an invalid attachment, by the SendGrid and Mailgun backends for a
non-success provider response, an unauthorized one included, and by the MailKit backend when the SMTP server
refuses the credentials, the sender, a recipient or the message. Log `ResponseContent` — the provider describes
the refusal there, and the status code on its own usually does not.

MailKit specifics:

- A message without a sender or recipients, or with an address or attachment content type MimeKit cannot parse,
  throws `MailException` before a connection is opened.
- **One refused recipient aborts the whole message** — nothing is delivered, not even to the accepted
  recipients — and `ResponseContent` names the refused address.
- A connection fault is not a refusal and passes through unwrapped: `SocketException` (no server),
  `SslHandshakeException`, `NotSupportedException` (`StartTls` against a server without STARTTLS, or a `UserName` for
  a server that offers no AUTH), `TimeoutException`, `OperationCanceledException`. A server that drops the session
  mid-exchange throws `MailException`, with the `SmtpProtocolException` inside.

| Property | Type | Description |
|---|---|---|
| `MessageObject` | `IMessageObject?` | The message that failed to send |
| `ResponseContent` | `string?` | Raw provider response body |

### `EmailFormatException`

| Property | Type | Description |
|---|---|---|
| `EmailInput` | `string?` | The invalid email address value |

---

## Testing — `DummyMailer`

Implements `IMailService` and does nothing. Use in tests to suppress actual sending:

```csharp
using Regira.Office.Mail.Abstractions;   // IMailService
using Regira.Office.Mail.Services;       // DummyMailer

services.AddSingleton<IMailService, DummyMailer>();
```

---

## Web DTOs — `MailInput`

Accept email requests over HTTP with `Mail.Web`:

<!-- no-compile -->
```csharp
[Authorize]
[HttpPost]
public async Task<IActionResult> Send([FromBody] MailInput input, IMailService mailer)
{
    var message = input.ToMessageObject();
    message.From = new MailAddress { Email = "no-reply@example.com" };   // the app's sender, not the caller's
    try
    {
        await mailer.Send(message);
        return Ok();
    }
    catch (MailException)   // Regira.Office.Mail.Exceptions: the provider or SMTP server refused the message
    {
        return StatusCode(502);
    }
}
```

`MailInput` and `MailInputExtensions.ToMessageObject()` are provided by `Regira.Office.Mail.Web`.

The caller chooses the recipients, the body and the attachments, so an anonymous endpoint relays spam or phishing
through the app's mail account. Never expose one without authorization, set `From` on the server rather than taking
`input.From`, and limit the recipients to the ones the app allows.

---

## Message File Reading

`Regira.Office.Mail.MSGReader` reads a stored message into an `IMessageObject`: `MsgParser` (`.msg`) and `EmlParser`
(`.eml`), both `IMessageParser`, parameterless and stateless, with no `Add…` extension:
`IMessageObject message = await new EmlParser().Parse(bytes.ToMemoryFile());`. `Body` is the HTML part when there is
one, else the text part; `IsHtml` is true only for the HTML part. `To` holds To, Cc and Bcc with their
`RecipientType`; only `EmlParser` fills `ReplyTo`. `EmlParser` expects CRLF line endings: convert a lone `\n` to
`\r\n` first, or a quoted-printable part keeps an `=` at each soft break and loses its line breaks.

---

## ASP.NET Identity Integration

`IdentityMailer` ships in `Regira.Security.Authentication.Web` and adapts any registered `IMailService` to
Identity's `IEmailSender`:

```csharp
using Microsoft.AspNetCore.Identity.UI.Services;          // IEmailSender
using Regira.Office.Mail.Abstractions;                    // IMailService
using Regira.Security.Authentication.Web.Mail;            // IdentityMailer, IdentityMailerOptions

services.AddSingleton<IEmailSender>(provider =>
    new IdentityMailer(
        provider.GetRequiredService<IMailService>(),
        new IdentityMailerOptions { Sender = "no-reply@example.com" }
    ));
```

It sends Identity's `htmlMessage` unchanged as an HTML body (`IsHtml` stays `true`), encoding nothing
(`security.instructions` → *PasswordControllerBase*).

---
