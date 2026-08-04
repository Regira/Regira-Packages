using System.Runtime.CompilerServices;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;
using SendGrid.Helpers.Mail;

[assembly: InternalsVisibleTo("Office.Mail.Testing")]
namespace Regira.Office.Mail.SendGrid.Extensions;

internal static class MessageObjectExtensions
{
    internal static SendGridMessage ToMailMessage(this IMessageObject messageObject)
    {
        var message = new SendGridMessage
        {
            // From
            From = messageObject.From?.ToMailAddress(),
            // Subject
            Subject = messageObject.Subject
        };

        if (messageObject.IsHtml)
        {
            // HTML
            message.HtmlContent = messageObject.Body;
        }
        else
        {
            // Plain text
            message.PlainTextContent = messageObject.Body;
        }

        // To
        var to = messageObject.To.Where(x => x.RecipientType == RecipientTypes.To);
        foreach (var mailAddress in to)
        {
            message.AddTo(mailAddress.ToMailAddress());
        }
        // CC
        var cc = messageObject.To.Where(x => x.RecipientType == RecipientTypes.Cc);
        foreach (var mailAddress in cc)
        {
            message.AddCc(mailAddress.ToMailAddress());
        }
        // BCC
        var bcc = messageObject.To.Where(x => x.RecipientType == RecipientTypes.Bcc);
        foreach (var mailAddress in bcc)
        {
            message.AddBcc(mailAddress.ToMailAddress());
        }
        // ReplyTo
        if (!string.IsNullOrEmpty(messageObject.ReplyTo?.Email))
        {
            var replyAddress = messageObject.ReplyTo!.ToMailAddress();
            message.SetReplyTo(replyAddress);
        }

        // Attachments
        if (messageObject.Attachments?.Any() == true)
        {
            message.Attachments = messageObject.Attachments
                    .Select(file => file.ToAttachment())
                    .ToList();
        }

        return message;
    }
}