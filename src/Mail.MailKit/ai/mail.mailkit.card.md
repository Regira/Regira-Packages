# Regira.Office.Mail.MailKit — index card

> Sends mail through any SMTP server with MailKit, behind the Regira `IMailService`. The guide lives on the hub
> `Regira.Office`: `get_package(id: "Regira.Office", section: "office.mail.instructions", heading: "DI Registration")`,
> with the `MailKitConfig` table under *Configuration* and the refusals under *Exceptions*.

- **`services.AddMailKit(cfg => …)`** (`Regira.Office.Mail.MailKit.DependencyInjection`) registers `MailKitMailer` as
  **`IMailService`** (`Regira.Office.Mail.Abstractions`), transient, with its `MailKitConfig` built from the configure
  action on every resolve.
- **`MailKitConfig`** (`Regira.Office.Mail.MailKit`): `Host`, `Port` (`587`), `Security` (`StartTls`, a
  `MailKit.Security.SecureSocketOptions`), `UserName`, `Password`, `Timeout` (per SMTP command, `120000` ms). A
  configuration section binds whole, `Security` by name:
  `services.AddMailKit(cfg => configuration.GetSection("Mail:Smtp").Bind(cfg))`.
- **Keep `StartTls` for a remote server:** it fails the send when the server offers no TLS, where `Auto` falls back to
  clear text and sends the password with it. A local test server without TLS (Mailpit, smtp4dev) takes
  `SecureSocketOptions.None`.
- **A refusal throws `MailException`** (`Regira.Office.Mail.Exceptions`): the credentials, the sender, a recipient or
  the message. One refused recipient aborts the whole message, and `ResponseContent` names the address. A connection
  fault (`SocketException`, `SslHandshakeException`, `TimeoutException`) passes through unwrapped.
- Each send opens its own connection (connect, authenticate, send, quit). OAuth2 sign-in is not supported.
- The message types (`MessageObject`, `MailAddress`, `MailRecipient`) are in `Regira.Office.Mail.Models`; every using:
  `get_package(id: "Regira.Office", section: "office.namespaces", heading: "Mail")`.
