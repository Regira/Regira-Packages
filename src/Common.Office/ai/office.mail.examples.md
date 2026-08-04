# Office.Mail — Example: Order Confirmation Email

> Context: A webshop sends an HTML order confirmation to the customer, CC-ing the sales team, with the invoice PDF attached.

## DI Registration

```csharp
// Program.cs
services.AddSendGrid(cfg => cfg.Key = configuration["Mail:SendGrid:Key"]!);
```

## Send order confirmation

```csharp
public async Task SendOrderConfirmation(Order order, IMemoryFile invoicePdf)
{
    var message = new MessageObject
    {
        From    = new MailAddress { Email = "orders@myshop.com", DisplayName = "MyShop" },
        Subject = $"Order confirmation #{order.Number}",
        Body    = await _htmlParser.Parse(OrderConfirmationTemplate, order),
        IsHtml  = true,
        To =
        [
            new MailRecipient { Email = order.CustomerEmail, DisplayName = order.CustomerName },
            new MailRecipient { Email = "sales@myshop.com",  RecipientType = RecipientTypes.Cc }
        ],
        Attachments =
        [
            new BinaryFileItem { FileName = $"invoice-{order.Number}.pdf", Bytes = invoicePdf.Bytes }
        ]
    };

    var result = await _mailer.Send(message);
    if (!result.Success)
        throw new MailException($"Failed to send order confirmation: {result.Content}");
}
```

## Send password-reset email

```csharp
public async Task SendPasswordReset(string email, string resetLink)
    => await _mailer.Send(
        sender:     "no-reply@myshop.com",
        recipients: [new MailRecipient { Email = email }],
        subject:    "Reset your password",
        message:    $"<p>Click <a href='{resetLink}'>here</a> to reset your password.</p>"
    );
```

## ASP.NET Identity integration

```csharp
services.AddSingleton<IEmailSender>(sp =>
    new IdentityMailer(
        sp.GetRequiredService<IMailService>(),
        new IdentityMailerOptions { Sender = "no-reply@myshop.com" }
    ));
```
