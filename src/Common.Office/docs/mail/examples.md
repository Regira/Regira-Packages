# Regira Office.Mail — Examples

## Example 1: Send a plain HTML message

The shortest possible send using implicit string conversions.

```csharp
await mailer.Send(
    sender:     "no-reply@example.com",
    recipients: ["alice@example.com"],
    subject:    "Order confirmed",
    message:    "<p>Your order <strong>#1042</strong> has been received.</p>"
);
```

---

## Example 2: Multiple recipients with Cc and Bcc

Build a `MessageObject` when you need fine-grained control over recipients, reply-to, or plain text.

```csharp
var message = new MessageObject
{
    From    = new MailAddress { Email = "orders@example.com", DisplayName = "Example Shop" },
    ReplyTo = "support@example.com",
    To      =
    [
        new MailRecipient { Email = "alice@example.com",   DisplayName = "Alice" },
        new MailRecipient { Email = "bob@example.com",     RecipientType = RecipientTypes.Cc },
        new MailRecipient { Email = "archive@example.com", RecipientType = RecipientTypes.Bcc },
    ],
    Subject = "Invoice #1042",
    Body    = "<h1>Invoice</h1><p>See the attached PDF.</p>",
    IsHtml  = true
};

var result = await mailer.Send(message);
if (!result.Success)
    logger.LogError("Mail failed: {Status} — {Content}", result.Status, result.Content);
```

---

## Example 3: Send with file attachments

Attach one or more files by passing `BinaryFileItem` instances in the `Attachments` collection.

```csharp
public async Task SendInvoice(IMailService mailer, string recipientEmail, byte[] pdfBytes)
{
    var message = new MessageObject
    {
        From    = "billing@example.com",
        To      = [new MailRecipient { Email = recipientEmail }],
        Subject = "Your invoice",
        Body    = "<p>Please find your invoice attached.</p>",
        Attachments =
        [
            new BinaryFileItem { FileName = "invoice.pdf", Bytes = pdfBytes, ContentType = "application/pdf" }
        ]
    };

    await mailer.Send(message);
}
```

---

## Example 4: Swap mail backend without changing application code

Drive the backend choice from configuration — consuming code only sees `IMailService`.

```csharp
var backend = configuration["Mail:Backend"];

if (backend == "mailgun")
{
    services.AddMailGun(cfg =>
    {
        cfg.Api    = configuration["Mail:MailGun:Api"]!;
        cfg.Key    = configuration["Mail:MailGun:Key"]!;
        cfg.Domain = configuration["Mail:MailGun:Domain"]!;
    });
}
else
{
    services.AddSendGrid(cfg =>
        cfg.Key = configuration["Mail:SendGrid:Key"]!);
}
```

---

## Example 5: Accept email requests via a JSON API endpoint

Use `MailInput` from `Mail.Web` to receive and validate a send request over HTTP.

```csharp
[ApiController]
[Route("mail")]
public class MailController(IMailService mailer) : ControllerBase
{
    [HttpPost("send")]
    public async Task<IActionResult> Send([FromBody] MailInput input)
    {
        var message = input.ToMessageObject();
        var result  = await mailer.Send(message);

        return result.Success
            ? Ok(new { result.Status })
            : StatusCode(502, new { result.Status, result.Content });
    }
}
```

A JSON body accepted by this endpoint:

```json
{
  "from":    { "email": "sender@example.com", "displayName": "Sender" },
  "to":      [{ "email": "alice@example.com" }],
  "subject": "Hello",
  "body":    "<p>Hi Alice!</p>",
  "isHtml":  true
}
```

---

## Example 6: ASP.NET Identity integration

Plug `IMailService` into the Identity email confirmation flow using `IdentityMailer`.

```csharp
// Program.cs
services.AddSendGrid(cfg => cfg.Key = configuration["Mail:SendGrid:Key"]!);

services.AddSingleton<IEmailSender>(provider =>
    new IdentityMailer(
        provider.GetRequiredService<IMailService>(),
        new IdentityMailerOptions
        {
            Sender = new MailAddress
            {
                Email       = "no-reply@example.com",
                DisplayName = "Example App"
            }
        }
    ));
```

`IEmailSender.SendEmailAsync` is now backed by your chosen `IMailService` implementation. All Identity-generated emails (confirmation, password reset, etc.) flow through SendGrid or Mailgun automatically.

---

## Overview

1. [Index](README.md) — Overview, interface, models, and configuration reference
1. **[Examples](examples.md)** — Simple send, attachments & multiple recipients, backend swap, Identity integration
