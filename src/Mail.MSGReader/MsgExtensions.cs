using MsgReader.Outlook;
using Regira.IO.Models;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.MSGReader;

public static class MsgExtensions
{
    public static IEnumerable<MailRecipient> ToRecipients(this IEnumerable<Storage.Recipient> recipients)
        => recipients.Select(r => ToRecipient(r, r.Type));

    public static MailRecipient ToRecipient(this Storage.Recipient recipient, RecipientType? type = null)
        => new MailRecipient
        {
            DisplayName = recipient.DisplayName,
            Email = recipient.Email,
            RecipientType = type switch
            {
                RecipientType.Cc => RecipientTypes.Cc,
                RecipientType.Bcc => RecipientTypes.Bcc,
                _ => RecipientTypes.To
            }
        };

    public static IEnumerable<BinaryFileItem> ToAttachments(this IEnumerable<Storage.Attachment?> items)
        => items
            .Where(x => x != null)
            .Select(ToFile!);

    public static BinaryFileItem ToFile(this Storage.Attachment item)
        => new BinaryFileItem
        {
            FileName = item.FileName,
            Length = item.Data?.Length ?? 0,
            ContentType = item.MimeType,
            Bytes = item.Data
        };
}