using Microsoft.AspNetCore.Identity.UI.Services;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Security.Authentication.Web.Mail;
public class IdentityMailer(IMailService mailer, IdentityMailerOptions options) : IEmailSender
{

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var msg = new MessageObject
        {
            From = options.Sender,
            To = { new MailRecipient { Email = email } },
            Subject = subject,
            Body = htmlMessage
        };
        return mailer.Send(msg, CancellationToken.None);
    }
}