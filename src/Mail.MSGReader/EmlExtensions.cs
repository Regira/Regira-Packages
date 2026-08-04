using MsgReader.Mime;
using MsgReader.Mime.Header;
using Regira.IO.Models;
using Regira.Office.Mail.Models;
using System.Collections.ObjectModel;

namespace Regira.Office.Mail.MSGReader;

public static class EmlExtensions
{
    public static IEnumerable<MailRecipient> ToRecipients(this IEnumerable<RfcMailAddress> recipients, RecipientTypes type = RecipientTypes.To)
        => recipients.Select(x => x.ToRecipient(type));
    public static MailRecipient ToRecipient(this RfcMailAddress recipient, RecipientTypes type = RecipientTypes.To)
        => new() { DisplayName = recipient.DisplayName, Email = recipient.Address, RecipientType = type };
    public static IEnumerable<BinaryFileItem> ToFiles(this ObservableCollection<MessagePart> attachments)
    {
        foreach (var messagePart in attachments)
        {
            yield return new BinaryFileItem
            {
                FileName = messagePart.FileName,
                Length = messagePart.Body?.Length ?? 0,
                ContentType = messagePart.ContentType?.ToString(),
                Bytes = messagePart.Body
            };
        }
    }
}