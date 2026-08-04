using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Models;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Office.Mail.Extensions;

public static class MessageObjectExtensions
{
    public static MailAddress ToMailAddress(this IMailAddress value)
        => new() { DisplayName = value.DisplayName, Email = value.Email };
    public static MailRecipient ToMailRecipient(this IMailRecipient value)
        => new() { DisplayName = value.DisplayName, Email = value.Email, RecipientType = value.RecipientType };
    public static IEnumerable<MailRecipient> ToMailRecipients(this IEnumerable<IMailRecipient> value)
        => value.Select(ToMailRecipient);

    public static BinaryFileItem ToFile(this INamedFile file)
        => (BinaryFileItem)file.ToBinaryFile();
    public static IEnumerable<BinaryFileItem> ToFiles(this IEnumerable<INamedFile> files)
        => files.Select(ToFile);
}