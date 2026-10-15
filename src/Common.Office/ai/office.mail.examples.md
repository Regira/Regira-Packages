# Office.Mail — Example: Order Confirmation Email

> Context: A webshop sends an HTML order confirmation to the customer, CC-ing the sales team, with the invoice PDF attached.

## DI Registration

```csharp
using Regira.Office.Mail.SendGrid.DependencyInjection;   // AddSendGrid

services.AddSendGrid(cfg => cfg.Key = configuration["Mail:SendGrid:Key"]!);
```

## Send order confirmation

<!-- no-compile -->
```csharp
using Regira.IO.Abstractions;                 // IMemoryFile
using Regira.IO.Extensions;                   // GetBytes()
using Regira.IO.Models;                       // BinaryFileItem
using Regira.Office.Mail.Abstractions;        // IMailService
using Regira.Office.Mail.Exceptions;          // MailException
using Regira.Office.Mail.Models;              // MessageObject, MailAddress, MailRecipient, RecipientTypes
using Regira.Web.HTML.Abstractions;           // IHtmlParser (package Regira.Web)

public class OrderMailer(IMailService mailer, IHtmlParser htmlParser)
{
    public async Task SendOrderConfirmation(Order order, IMemoryFile invoicePdf)
    {
        var message = new MessageObject
        {
            From    = new MailAddress { Email = "orders@myshop.com", DisplayName = "MyShop" },
            Subject = $"Order confirmation #{order.Number}",
            Body    = await htmlParser.Parse(OrderConfirmationTemplate, order),
            IsHtml  = true,
            To =
            [
                new MailRecipient { Email = order.CustomerEmail, DisplayName = order.CustomerName },
                new MailRecipient { Email = "sales@myshop.com",  RecipientType = RecipientTypes.Cc }
            ],
            Attachments =
            [
                new BinaryFileItem { FileName = $"invoice-{order.Number}.pdf", Bytes = invoicePdf.GetBytes() }
            ]
        };

        var result = await mailer.Send(message);
        if (!result.Success)
            throw new MailException($"Failed to send order confirmation: {result.Content}");
    }
}
```

## Send password-reset email

<!-- no-compile -->
```csharp
public async Task SendPasswordReset(string email, string resetLink)
    => await _mailer.Send(
        sender:     new MailAddress { Email = "no-reply@myshop.com" },
        recipients: [new MailRecipient { Email = email }],
        subject:    "Reset your password",
        message:    $"<p>Click <a href='{resetLink}'>here</a> to reset your password.</p>"
    );
```

## ASP.NET Identity integration

```csharp
using Microsoft.AspNetCore.Identity.UI.Services;          // IEmailSender
using Regira.Office.Mail.Abstractions;                    // IMailService
using Regira.Security.Authentication.Web.Mail;            // IdentityMailer (package Regira.Security.Authentication.Web)

services.AddSingleton<IEmailSender>(sp =>
    new IdentityMailer(
        sp.GetRequiredService<IMailService>(),
        new IdentityMailerOptions { Sender = "no-reply@myshop.com" }
    ));
```
