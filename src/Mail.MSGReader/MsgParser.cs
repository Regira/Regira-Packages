using System.Text;
using MsgReader.Outlook;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.MSGReader;

public class MsgParser : IMessageParser
{
    static MsgParser()
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
    }

    public Task<IMessageObject> Parse(IMemoryFile msgFile, CancellationToken cancellationToken = default)
    {
        using var msgStream = msgFile.GetStream();
        using var msg = new Storage.Message(msgStream);

        return Task.FromResult<IMessageObject>(new MessageObject
        {
            From = new MailAddress { DisplayName = msg.Sender.DisplayName, Email = msg.Sender.Email },
            To = msg.Recipients
                .ToRecipients()
                .ToList(),
            Subject = msg.Subject,
            Body = msg.BodyHtml ?? msg.BodyText,
            IsHtml = !string.IsNullOrWhiteSpace(msg.BodyHtml),
            Attachments = msg.Attachments
                .Cast<Storage.Attachment?>()
                .ToAttachments()
                .ToList()
        });
    }
}