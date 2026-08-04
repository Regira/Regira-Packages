# Regira Office.Mail AI Agent Instructions

---

## Module Context

Part of **Regira Office**. For routing and full module overview, see [`office.instructions.md`](./office.instructions.md).

| Namespace | Covers |
|---|---|
| `Regira.Office.Mail` | Email sending, mail DTOs for HTTP endpoints, and `.msg` / `.eml` reading |

**Related:**
- [IO.Storage](../../Common.IO.Storage/ai/io.storage.instructions.md) — `INamedFile` used for email attachments

---

## Installation

```xml
<!-- SendGrid -->
<PackageReference Include="Regira.Office.Mail.SendGrid" Version="6.*" />

<!-- Mailgun -->
<PackageReference Include="Regira.Office.Mail.MailGun" Version="6.*" />

<!-- Mail.Web — HTTP request DTOs for mail endpoints -->
<PackageReference Include="Regira.Office.Mail.Web" Version="6.*" />

<!-- MSGReader — read existing .msg and .eml files -->
<PackageReference Include="Regira.Office.Mail.MSGReader" Version="6.*" />
```

---

## `IMailService`

Both backends implement this interface.

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
| `Success` | `bool` | `true` when the provider accepted the message |
| `Status` | `string?` | HTTP status code or provider status text |
| `Content` | `string?` | Raw response body |
| `Exception` | `Exception?` | Set when sending fails |

---

## Core Models

### `IMessageObject` / `MessageObject`

| Property | Type | Default | Description |
|---|---|---|---|
| `From` | `IMailAddress?` | `null` | Sender address |
| `To` | `ICollection<IMailRecipient>` | `[]` | Recipients (To / Cc / Bcc) |
| `ReplyTo` | `IMailAddress?` | `null` | Reply-To address |
| `Subject` | `string?` | `null` | Email subject |
| `Body` | `string?` | `null` | Message body |
| `IsHtml` | `bool` | `true` | HTML vs plain text |
| `Attachments` | `ICollection<INamedFile>?` | `null` | File attachments |

### `IMailAddress` / `MailAddress`

| Property | Type | Description |
|---|---|---|
| `Email` | `string` | Email address — validated on assignment |
| `DisplayName` | `string?` | Optional display name |

```csharp
MailAddress addr  = "alice@example.com";  // implicit from string
MailAddress named = new() { Email = "alice@example.com", DisplayName = "Alice" };
```

`ToString()` returns `"Alice <alice@example.com>"` when `DisplayName` is set.

### `IMailRecipient` / `MailRecipient`

Extends `IMailAddress` with a recipient type.

```csharp
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

---

## DI Registration

```csharp
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
```

Both extension methods register `IMailService` as a transient service.

---

## Exceptions

### `MailException`

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
services.AddSingleton<IMailService, DummyMailer>();
```

---

## Web DTOs — `MailInput`

Accept email requests over HTTP with `Mail.Web`:

```csharp
[HttpPost]
public async Task<IActionResult> Send([FromBody] MailInput input, IMailService mailer)
{
    var message = input.ToMessageObject();
    var result  = await mailer.Send(message);
    return result.Success ? Ok() : StatusCode(502);
}
```

`MailInput` and `MailInputExtensions.ToMessageObject()` are provided by `Regira.Office.Mail.Web`.

---

## Message File Reading

Use `Regira.Office.Mail.MSGReader` when the task is reading existing `.msg` or `.eml` files rather than sending mail. This guide does not define its concrete reader API, so inspect the package source before relying on specific types or methods.

---

## ASP.NET Identity Integration

```csharp
services.AddSingleton<IEmailSender>(provider =>
    new IdentityMailer(
        provider.GetRequiredService<IMailService>(),
        new IdentityMailerOptions { Sender = "no-reply@example.com" }
    ));
```

---
