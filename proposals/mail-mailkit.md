# An SMTP mail backend on MailKit

As of 2026-10-07. Sources: the `Regira-Packages` repository, branch `wip` at `e2a10c7`; the nuget.org metadata of MailKit 4.18.0 (published 2026-09-13), MimeKit 4.18.0/4.18.1, MailKitLite 4.18.0 and BouncyCastle.Cryptography 2.7.0, read on 2026-10-07; the article [Sending Email in .NET with MailKit](https://thomasngoswe.com/2026/03/07/dotnet-mailkit-smtp-guide/); and a throwaway spike that sent through MailKit 4.18.0 on .NET 10 to a local Mailpit v1.31.4 container (see *What the spike showed*). Nothing on this page has been built.

**Status: built on 2026-10-08 for 6.5.1, uncommitted. All seven decisions were built as recommended; *Outcome* records where the build differs from the design.**

## Recommendation

Add `Regira.Office.Mail.MailKit`, a third sending backend beside Mail.SendGrid and Mail.MailGun. Its mailer derives from `MailerBase`, maps an `IMessageObject` to a MimeKit `MimeMessage`, and sends it through MailKit's `SmtpClient` to any SMTP server: a company relay, Microsoft 365, Gmail with an app password, Amazon SES or Mailgun's SMTP endpoint, or a local catch-all server in development.

The gap this closes: every backend today needs an account with a specific HTTP mail API. An application that already has an SMTP server, or must not send mail through a third party, has no Regira backend. It also gives development and tests a real send to a local Mailpit or smtp4dev with no account and no credentials. `DummyMailer` does not send at all, and Mailgun's `TestMode` still needs a Mailgun account.

MailKit is the library to use. It is MIT licensed, maintained, targets `net8.0` and `net10.0` directly, and Microsoft's own documentation steers new code away from `System.Net.Mail.SmtpClient` towards it.

The steps, in order:

1. The package: config, the `MimeMessage` mapping, the mailer, the DI extension.
2. Offline tests of the mapping, then a Mailpit fixture that sends through the shared `MailerTestsBase`.
3. Guides, docs, routing tables, the solution, GuideVerifier and the changelog.

## What exists today

| What | Where | Effect |
|---|---|---|
| `MailerBase` checks attachments, then calls `OnSend` | `src/Common.Office/Mail/Abstractions/MailerBase.cs` | The new mailer implements one method, as both siblings do |
| Each sibling is a mailer, a config class and an `Add{Provider}` extension | `SendGridMailer` / `SendGridConfig` / `AddSendGrid`; `MailGunMailer` / `MailgunConfig` / `AddMailGun` | Registration is `Configure<TConfig>` plus a transient config resolved from `IOptionsSnapshot<TConfig>`, plus a transient `IMailService`. The new package copies this shape |
| SendGrid's mapping is an internal extension, tested offline | `Mail.SendGrid/Extensions/MessageObjectExtensions.cs` with `InternalsVisibleTo("Office.Mail.Testing")`; `SendGridMessageObjectTests` over the `MailConstants` JSON inputs | The new `ToMimeMessage()` follows that shape. MimeKit exposes `To`, `Cc` and `Bcc`, which SendGrid's message does not, so recipients can be asserted too |
| A refused send throws `MailException` with `ResponseContent`; an unauthorized one throws a plain `Exception` | Both siblings; the *Exceptions* section of `office.mail.instructions.md` | The SMTP error mapping has a contract to follow (decision 4) |
| The shared send tests take a ready `IMailService` | `MailerTestsBase.Send_Without_Attachment` / `Send_With_Attachment` | One more fixture. `MailGunTests` shows the skip-when-unconfigured pattern |
| Container tests have a pattern | `PgDatabaseTests`: Testcontainers, category `Containers`, runs only when `REGIRA_PROVIDER_TESTS=containers`, skips when Docker is unavailable | A Mailpit fixture follows it (decision 6) |
| Mail packages ship at 6.5.1 | `Mail.SendGrid`, `Mail.MailGun`, `Mail.MSGReader`, `Mail.Web`, `Common.Office` | The new package starts on the family's number |

## What the spike showed

MailKit 4.18.0 sent to a Mailpit container with SMTP AUTH enabled and recipients restricted to one domain. Each row is one send.

| Case | Result |
|---|---|
| A message with a named From, a named To, a Cc, a Bcc, a named Reply-To, an HTML body and a PDF attachment | Delivered. `SendAsync` returns the server's reply, `2.0.0 Ok: queued as …`. Display names, Reply-To and the attachment arrive intact; MimeKit adds a `Message-Id` |
| Wrong password | `MailKit.Security.AuthenticationException`: `535: 5.7.8 Authentication credentials invalid` |
| No `AuthenticateAsync` against a server that requires it | `MailKit.ServiceNotAuthenticatedException`: `5.7.0 Authentication required` |
| One recipient refused by the server | `MailKit.Net.Smtp.SmtpCommandException`, `ErrorCode = RecipientNotAccepted`, `StatusCode = MailboxUnavailable`, `Mailbox` = the refused address. **Nothing is delivered**, not even to the accepted Cc and Bcc |
| No recipients | `InvalidOperationException`: `No recipients have been specified.` (after connecting and authenticating) |
| No sender | `InvalidOperationException`: `No sender has been specified.` (likewise) |
| `SecureSocketOptions.StartTls` against a server without STARTTLS | `NotSupportedException`: `The SMTP server does not support the STARTTLS extension.` |
| `SecureSocketOptions.SslOnConnect` against a plain port | `MailKit.Security.SslHandshakeException` |
| Closed port | `System.Net.Sockets.SocketException` |
| `SecureSocketOptions.Auto` against a plain server on a non-465 port | Connects without TLS **and authenticates in clear text** |
| `SmtpClient.Timeout` | 120,000 ms by default |
| `SmtpClient.OnRecipientNotAccepted` | `protected virtual` — a subclass can skip a refused recipient instead of aborting |

Mailpit showed the Bcc recipient as a header prepended to the raw message; MailKit hides `Bcc` from the transmitted headers and sends that recipient only in the envelope.

## Design

### Package

`src/Mail.MailKit/`, package `Regira.Office.Mail.MailKit`, `net8.0;net10.0`, version 6.5.1, referencing `Common.Office`, `Microsoft.Extensions.Options` 10.0.12 and `MailKit` 4.18.0. Names follow decision 1.

### Config

<!-- no-compile -->
```csharp
public class MailKitConfig
{
    public string Host { get; set; } = null!;
    public int Port { get; set; } = 587;
    /// <summary>StartTls by default: refuses to send, rather than authenticate in clear text, when the server offers no TLS.</summary>
    public SecureSocketOptions Security { get; set; } = SecureSocketOptions.StartTls;
    /// <summary>Leave empty for a server that takes mail without SMTP AUTH (an internal relay, a local test server).</summary>
    public string? UserName { get; set; }
    public string? Password { get; set; }
    /// <summary>Per SMTP command; MailKit's default.</summary>
    public int Timeout { get; set; } = 120_000;
}
```

| Server | `Port` | `Security` |
|---|---|---|
| Submission with STARTTLS (Microsoft 365, Gmail, SES, most providers) | 587 | `StartTls` |
| Implicit TLS | 465 | `SslOnConnect` |
| Local Mailpit / smtp4dev | 1025 / 25 | `None` |

### Mapping — internal `ToMimeMessage()`

| `IMessageObject` | `MimeMessage` |
|---|---|
| `From` | `From.Add(new MailboxAddress(DisplayName, Email))` |
| `To` by `RecipientType` | `To`, `Cc`, `Bcc` |
| `ReplyTo` | `ReplyTo` when it has an email |
| `Subject` | `Subject` |
| `Body` + `IsHtml` | `BodyBuilder.HtmlBody` or `.TextBody`; a null body is an empty text part |
| `Attachments` | `BodyBuilder.Attachments.Add(FileName, GetBytes(), ContentType.Parse(ContentType ?? ContentTypeUtility.GetContentType(FileName)))` — the content-type fallback both siblings use |

A missing `From` or an empty recipient list throws `MailException` from the mapping, before any connection. MailKit's own check comes only after connecting and authenticating, and throws `InvalidOperationException`.

### Sending

A new `SmtpClient` per send: connect, authenticate when `UserName` is set, send, disconnect with `QUIT`, dispose. `SmtpClient` is not thread-safe, and a transient mailer with no shared connection matches the siblings' lifetime. The cost is one TCP, TLS and AUTH handshake per message, which is fine for transactional mail and slow for bulk sends. Connection reuse is left for later (see *Not in scope*).

The `CancellationToken` passes to `ConnectAsync`, `AuthenticateAsync`, `SendAsync` and `DisconnectAsync`.

### Response

`Success = true`, `Status = "OK"`, `Content` = the server's reply to `DATA` (`2.0.0 Ok: queued as …`), which carries the server's queue id.

### Errors

Following the siblings' contract (decision 4):

| MailKit throws | The mailer throws |
|---|---|
| `SmtpCommandException` — sender, recipient or message refused | `MailException("Sending message failed. Status: {StatusCode}", ex)` with `MessageObject` and `ResponseContent` = the server's reply text |
| `SmtpProtocolException` | `MailException` likewise |
| `AuthenticationException`, `ServiceNotAuthenticatedException` | `MailException` likewise — as the siblings throw for an unauthorized response (see *Outcome*) |
| `SocketException`, `SslHandshakeException`, `NotSupportedException` (no STARTTLS), `TimeoutException`, `OperationCanceledException` | Unchanged — a configuration or network fault, not a refused message |

### DI

<!-- no-compile -->
```csharp
services.AddMailKit(cfg =>
{
    cfg.Host     = configuration["Mail:Smtp:Host"]!;
    cfg.Port     = int.Parse(configuration["Mail:Smtp:Port"] ?? "587");
    cfg.UserName = configuration["Mail:Smtp:UserName"];
    cfg.Password = configuration["Mail:Smtp:Password"];
});
```

## Tests

- **Mapping, offline.** `MailKitMessageObjectTests` over the `MailConstants` inputs, as `SendGridMessageObjectTests` does, plus the From/recipient guard. It runs everywhere.
- **Send, Mailpit container.** `MailKitTests : MailerTestsBase` against a Mailpit container started by Testcontainers, in category `Containers`, gated on `REGIRA_PROVIDER_TESTS=containers`, skipping without Docker. Mailpit's HTTP API (`/api/v1/messages`) lets the fixture assert what arrived — recipients by kind, display names, Reply-To, the attachment — which the HTTP backends' fixtures cannot. With Mailpit's SMTP AUTH and allowed-recipients settings, the same fixture also covers the wrong-password and refused-recipient rows above.
- **Real server, optional.** The Mailgun pattern: `Mail:Smtp:*` user secrets, skipped when absent, category `Network`.

## Files to change

| File | Change |
|---|---|
| `src/Mail.MailKit/` | New: `.csproj`, `MailKitConfig`, `MailKitMailer`, `Extensions/MessageObjectExtensions.cs`, `DependencyInjection/ServiceCollectionExtensions.cs`, `README.md` (copy Mail.MailGun's) |
| `src/Common.Office/ai/office.mail.instructions.md` | Installation, the config table with the port/security table, DI, the error mapping, the all-or-nothing recipient rule; "Both backends" becomes "Every backend" |
| `src/Common.Office/ai/office.namespaces.md` | The Providers and DI rows |
| `src/Common.Office/docs/mail/README.md`, `docs/mail/examples.md` | Projects table, installation, configuration, DI; Example 4's backend switch gains an `smtp` branch |
| `src/Common.Office/README.md` | The Mail row ("via SendGrid and Mailgun") |
| `ai/AGENTS.md`, `src/Common.Setup/ai/copilot-instructions.md` | The `Office.Mail` routing rows, saying when to pick SMTP. Both files have uncommitted edits from other work on `wip` today |
| `Regira-Packages.slnx` | The `/src/Office/Mail/` folder. Also has uncommitted edits on `wip` |
| `tools/GuideVerifier/projects.json` | The `office-mail` group's project list |
| `tests/Office.Mail.Testing/` | Project reference, `Testcontainers` 4.15.0 (the version the PostgreSQL tests pin), the two fixtures, `README.md` |
| `CHANGELOG.md` | `` `Regira.Office.Mail.MailKit` 6.5.1 — SMTP backend on MailKit `` under `## Unreleased` |

Regira-Website's `packages.json` picks the package up from `src/` the next time `npm run packages` runs there.

## Decisions

1. **Names.** Recommended: package `Regira.Office.Mail.MailKit`, `MailKitMailer`, `MailKitConfig`, `AddMailKit` — the library names the provider, as in `Excel.EPPlus` or `Word.Mini`, and the class follows its siblings' `{Provider}Mailer` on `MailerBase`. The `{Family}Service` convention would give `MailService`, which collides with MailKit's own `MailKit.MailService` base class for any consumer importing both namespaces. Alternative: `Mail.Smtp` / `SmtpMailer` / `SmtpConfig` / `AddSmtp`, which names the protocol a consumer searches for; the package tags carry `smtp` either way.
2. **Default `Security`.** Recommended: `StartTls`, which fails closed. MailKit's `Auto` falls back to plain text on a server without STARTTLS and then sends the password in the clear (spike). Local servers set `None` explicitly.
3. **MailKit's `SecureSocketOptions` in the public config, or an own enum.** Recommended: MailKit's. The package depends on MailKit anyway, it binds from configuration by name (`"StartTls"`), and an own enum would duplicate its five values.
4. **Error mapping.** Recommended: as in *Errors* — a server refusal is a `MailException`, authentication and connection faults pass through unchanged. Alternative: wrap everything in `MailException`, which is uniform but hides the difference between "the server said no" and "the configuration is wrong".
5. **A refused recipient.** Recommended: all-or-nothing, MailKit's default — the send aborts and the `MailException` names the refused address. Alternative: override `OnRecipientNotAccepted` to skip it and deliver to the rest, which then needs a way to report the skipped addresses that `IMailResponse` does not have.
6. **MailKit or MailKitLite.** Recommended: MailKit. MailKitLite drops MimeKit's S/MIME, PGP and DKIM support and with it BouncyCastle.Cryptography (an 8.7 MB package) and `System.Security.Cryptography.Pkcs`. But an application that already uses MailKit, for IMAP for instance, would then carry both, and DKIM signing would no longer be available as a later option.

7. **The Mailpit fixture.** Recommended: yes — it is the only test that sends for real without an account. It adds `Testcontainers` to the mail test project and runs only on machines that opt in. Without it, the mapping tests and the user-secrets fixture remain.

## Not in scope

- **OAuth2** (`SaslMechanismOAuth2`) for Microsoft 365 and Gmail tenants that have basic SMTP AUTH disabled. It needs a token source on the config, such as `Func<CancellationToken, Task<string>>`, and can be added as an opt-in member later.
- **Connection reuse** for bulk sending: a pooled or batched client behind the same `IMailService`.
- **DKIM signing** (`MimeKit.Cryptography.DkimSigner`). Most relays sign on the server side.
- **A switch to accept invalid server certificates.** A self-signed development server runs with `Security = None` on a local port.

## Outcome

Built as designed, with these differences:

- **Authentication failures are a `MailException`.** While this was built, SendGrid and Mailgun moved an unauthorized response from a plain `Exception` to `MailException` (same 6.5.1). The MailKit mailer follows: a refused login (`AuthenticationException`) or a server that requires one (`ServiceNotAuthenticatedException`) is wrapped, with the server's reply on `ResponseContent`. Connection faults still pass through unwrapped.
- **A refused recipient's address is on `ResponseContent`.** The server's reply (`5.1.0 Requested action not taken: mailbox unavailable`) does not name the address, so the mailer appends `SmtpCommandException.Mailbox`.
- **A failed `QUIT` is ignored.** The server has accepted the message by then; reporting the send as failed would invite a duplicate.
- **The Mailpit fixture runs by default.** The repo's `Regira.runsettings` sets `REGIRA_PROVIDER_TESTS=containers`, so the fixture runs on every machine with Docker and skips without it. The image is pinned to `axllent/mailpit:v1.31.4`.
- **Mailgun's dropped display names and Reply-To**, noticed during the investigation, were fixed separately in the same 6.5.1.

Tests: `MailKitMessageObjectTests` (10, offline) and `MailKitTests` (6, Mailpit) pass, as does the rest of `Office.Mail.Testing`; GuideVerifier compiles all 16 `office-mail` snippets.
