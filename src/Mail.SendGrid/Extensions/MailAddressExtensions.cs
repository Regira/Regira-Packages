using Regira.Office.Mail.Abstractions;
using SendGrid.Helpers.Mail;

namespace Regira.Office.Mail.SendGrid.Extensions;

internal static class MailAddressExtensions
{
    internal static EmailAddress ToMailAddress(this IMailAddress mailAddress)
    {
        return !string.IsNullOrEmpty(mailAddress.DisplayName)
            ? new EmailAddress(mailAddress.Email, mailAddress.DisplayName)
            : new EmailAddress(mailAddress.Email);
    }
}