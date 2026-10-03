# Regira.Office.Mail.MailGun

Mailgun backend for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) mail, sending through the Mailgun REST API with [RestSharp](https://www.nuget.org/packages/RestSharp). `MailGunMailer` implements `IMailService`, and `services.AddMailGun(...)` configures `MailgunConfig` and registers the mailer as a transient `IMailService`. `MailgunConfig.TestMode` has Mailgun accept and log each message without delivering it.

## Installation

```xml
<PackageReference Include="Regira.Office.Mail.MailGun" Version="6.*" />
```

Requires a Mailgun account: `MailgunConfig` takes the API endpoint, an API key and the sending domain.

## Documentation

- [Mail](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/) — `IMailService`, the message models, `MailgunConfig` and test mode, DI registration, and the exceptions a refused send throws
- [Mail examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/examples.html) — sending with recipients and attachments, and choosing the backend from configuration

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
