# Regira.Office.Mail.Web

HTTP request model for [Regira Office](https://regira.github.io/Regira-Packages/src/Common.Office/) mail. `MailInput` is a data-annotated DTO for a send request — sender, recipients typed To, Cc or Bcc, reply-to, subject, body and attachments — and `ToMessageObject()` converts it to the `IMessageObject` any `IMailService` sends. It sends nothing itself; pair it with a mail backend such as [Regira.Office.Mail.SendGrid](https://regira.github.io/Regira-Packages/src/Mail.SendGrid/) or [Regira.Office.Mail.MailGun](https://regira.github.io/Regira-Packages/src/Mail.MailGun/).

## Installation

```xml
<PackageReference Include="Regira.Office.Mail.Web" Version="6.*" />
```

## Documentation

- [Mail — Web DTOs](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/#web-dtos--mailinput) — the `MailInput` structure and its validation, and a controller action that sends it
- [Mail examples](https://regira.github.io/Regira-Packages/src/Common.Office/docs/mail/examples.html) — a JSON endpoint built on `MailInput`, with a sample request body

## License

Apache License 2.0 — this package contains no license validation and no runtime limits. See [LICENSE](https://github.com/Regira/Regira-Packages/blob/main/LICENSE). A few companion packages are commercially licensed with a free tier; see the [licensing overview](https://regira.github.io/Regira-Packages/licensing.html).
