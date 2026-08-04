using Regira.IO.Abstractions;

namespace Regira.Office.Mail.Abstractions;

public interface IMessageObject
{
    IMailAddress? From { get; set; }
    ICollection<IMailRecipient> To { get; set; }
    IMailAddress? ReplyTo { get; set; }
    string? Subject { get; set; }
    string? Body { get; set; }
    bool IsHtml { get; set; }
    ICollection<INamedFile>? Attachments { get; set; }
}