# Regira.Office.Mail.SendGrid

SendGrid backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) mail, built on the [SendGrid](https://www.nuget.org/packages/Sendgrid) client. `SendGridMailer` implements `IMailService`, and `services.AddSendGrid(...)` configures `SendGridConfig` and registers the mailer as a transient `IMailService`.

## Installation

```xml
<PackageReference Include="Regira.Office.Mail.SendGrid" Version="6.*" />
```

Requires a SendGrid account: `SendGridConfig.Key` takes its API key.

## Documentation

- [Mail](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/) — `IMailService`, the message models, `SendGridConfig`, DI registration, and the exceptions a refused send throws
- [Mail examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/examples.html) — sending with recipients and attachments, choosing the backend from configuration, and ASP.NET Identity emails

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
