using Regira.IO.Abstractions;
using Regira.IO.Models;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Extensions;

namespace Regira.Office.Mail.Models;

public class MessageObject : IMessageObject
{
    public MailAddress? From { get; set; }
    public ICollection<MailRecipient> To { get; set; } = new List<MailRecipient>();
    public IMailAddress? ReplyTo { get; set; }
    public string? Subject { get; set; }
    public string? Body { get; set; }
    public bool IsHtml { get; set; } = true;
    public ICollection<BinaryFileItem>? Attachments { get; set; }

    IMailAddress? IMessageObject.From
    {
        get => From;
        set => From = value?.ToMailAddress();
    }
    ICollection<IMailRecipient> IMessageObject.To
    {
        get => To.Cast<IMailRecipient>().ToList();
        set => To = value.ToMailRecipients().ToList();
    }
    ICollection<INamedFile>? IMessageObject.Attachments
    {
        get => Attachments?.Cast<INamedFile>().ToList();
        set => Attachments = value?.ToFiles().ToList();
    }
}