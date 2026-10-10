# Office.Mail.Testing

Tests for the mail packages: message models and JSON input validation ([Mail.Web](../../src/Mail.Web/README.md)),
parsing `.msg` and `.eml` files ([Mail.MSGReader](../../src/Mail.MSGReader/README.md)), and sending through
[Mail.MailGun](../../src/Mail.MailGun/README.md), [Mail.SendGrid](../../src/Mail.SendGrid/README.md) and
[Mail.MailKit](../../src/Mail.MailKit/README.md). NUnit.

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

Skip both with `--filter "TestCategory!=Network"`. `SendGridMessageObjectTests`, `MailGunRequestTests` and
`MailKitMessageObjectTests` check the request or MIME message each backend builds — addresses, display names,
Reply-To, body — offline.

`MailKitTests` (category `Containers`) needs Docker. It runs when `REGIRA_PROVIDER_TESTS=containers`, which the
repo's `Regira.runsettings` sets, and skips when Docker is unavailable. It sends to a Mailpit container
(`axllent/mailpit`) that requires SMTP AUTH and accepts a few recipient domains only, then reads back through Mailpit's
HTTP API what arrived: recipients by kind, display names, Reply-To and the attachment. It also covers a wrong password,
a refused recipient, and the `StartTls` default against a server without TLS. Skip it with
`--filter "TestCategory!=Containers"`.
