using MimeKit;
using Regira.IO.Extensions;
using Regira.IO.Utilities;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Exceptions;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.MailKit.Extensions;

internal static class MessageObjectExtensions
{
    /// <summary>
    /// Maps the message to MIME. A missing sender or recipient throws here, before a connection is opened;
    /// MailKit itself notices only after connecting and authenticating.
    /// </summary>
    internal static MimeMessage ToMimeMessage(this IMessageObject messageObject)
    {
        if (string.IsNullOrWhiteSpace(messageObject.From?.Email))
        {
            throw new MailException("The message has no sender") { MessageObject = messageObject };
        }
        if (messageObject.To.Count == 0)
        {
            throw new MailException("The message has no recipients") { MessageObject = messageObject };
        }

        var message = new MimeMessage
        {
            Subject = messageObject.Subject ?? string.Empty
        };
        message.From.Add(messageObject.From.ToMailboxAddress());
        foreach (var recipient in messageObject.To)
        {
            var list = recipient.RecipientType switch
            {
                RecipientTypes.Cc => message.Cc,
                RecipientTypes.Bcc => message.Bcc,
                _ => message.To
            };
            list.Add(recipient.ToMailboxAddress());
        }
        if (!string.IsNullOrWhiteSpace(messageObject.ReplyTo?.Email))
        {
            message.ReplyTo.Add(messageObject.ReplyTo.ToMailboxAddress());
        }

        var body = new BodyBuilder();
        if (messageObject.IsHtml)
        {
            body.HtmlBody = messageObject.Body ?? string.Empty;
        }
        else
        {
            body.TextBody = messageObject.Body ?? string.Empty;
        }
        if (messageObject.Attachments != null)
        {
            foreach (var file in messageObject.Attachments)
            {
                var contentType = file.ContentType ?? ContentTypeUtility.GetContentType(file.FileName!);
                body.Attachments.Add(file.FileName!, file.GetBytes()!, ContentType.Parse(contentType));
            }
        }
        message.Body = body.ToMessageBody();

        return message;
    }

    internal static MailboxAddress ToMailboxAddress(this IMailAddress mailAddress)
        => new(mailAddress.DisplayName, mailAddress.Email);
}
