using Regira.IO.Models;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.Web;

public static class MailInputExtensions
{
    public static IMessageObject ToMessageObject(this MailInput input)
    {
        return new MessageObject
        {
            From = input.From.ToMailAddress()!,
            To = input.To?.Select(ToMailRecipient).ToList() ?? [],
            ReplyTo = input.ReplyTo?.ToMailAddress(),
            Subject = input.Subject,
            Body = input.Body,
            IsHtml = input.IsHtml,
            Attachments = input.Attachments
                ?.Select(x => new BinaryFileItem
                {
                    FileName = x.FileName,
                    Bytes = x.Bytes,
                    ContentType = x.ContentType
                })
                .ToList()
        };
    }
    public static MailAddress? ToMailAddress(this MailInput.Address? address)
    {
        if (address == null)
        {
            return null;
        }

        return new MailAddress
        {
            DisplayName = address.DisplayName,
            Email = address.Email,
        };
    }
    public static MailRecipient ToMailRecipient(this MailInput.Recipient address)
    {
        return new MailRecipient
        {
            DisplayName = address.DisplayName,
            Email = address.Email,
            RecipientType = address.RecipientType
        };
    }
}