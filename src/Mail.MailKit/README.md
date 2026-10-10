# Regira.Office.Mail.MailKit

SMTP backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) mail, built on [MailKit](https://www.nuget.org/packages/MailKit). `MailKitMailer` implements `IMailService` and sends to any SMTP server: a company relay, Microsoft 365, Gmail with an app password, Amazon SES, or a local test server such as Mailpit. `services.AddMailKit(...)` configures `MailKitConfig` and registers the mailer as a transient `IMailService`.

## Installation

```xml
<PackageReference Include="Regira.Office.Mail.MailKit" Version="6.*" />
```

Requires an SMTP server: `MailKitConfig` takes its host and port, the TLS mode (STARTTLS by default) and, when the server requires SMTP AUTH, a user name and password.

## Documentation

- [Mail](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/) — `IMailService`, the message models, `MailKitConfig` and its TLS modes, DI registration, and the exceptions a refused send throws
- [Mail examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/examples.html) — sending with recipients and attachments, and choosing the backend from configuration

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
