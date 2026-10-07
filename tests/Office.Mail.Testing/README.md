# Office.Mail.Testing

Tests for the mail packages: message models and JSON input validation ([Mail.Web](../../src/Mail.Web/README.md)),
parsing `.msg` and `.eml` files ([Mail.MSGReader](../../src/Mail.MSGReader/README.md)), and sending through
[Mail.MailGun](../../src/Mail.MailGun/README.md) and [Mail.SendGrid](../../src/Mail.SendGrid/README.md). NUnit.

## Running

```bash
dotnet test tests/Office.Mail.Testing
```

`MailGunTests` and `SendGridTests` are in the `Network` category and read user secrets:

- Mailgun: `Mail:MailGun:Api`, `Mail:MailGun:Key` and `Mail:MailGun:Domain`. If any is missing the tests are
  skipped; with all three they call Mailgun in test mode, which accepts and logs the message without delivering it,
  one of them with quoted display names and a Reply-To header.
- SendGrid: both tests are always ignored, but the fixture builds its client from `Mail:SendGrid:Key` and fails
  when that secret is absent.

Skip both with `--filter "TestCategory!=Network"`. `SendGridMessageObjectTests` and `MailGunRequestTests` check the
request each backend builds — addresses, display names, Reply-To, body — offline.
