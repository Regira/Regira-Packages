using Regira.Office.Mail.Models;

namespace Regira.Security.Authentication.Web.Mail;

public class IdentityMailerOptions
{
    public MailAddress Sender { get; set; } = null!;
    /// <summary>
    /// Reads each message as plain text, the form the account controllers write their mails in: the text is
    /// HTML-encoded, every <c>http://</c> or <c>https://</c> link becomes an anchor and every line break a <c>&lt;br&gt;</c>.
    /// Off by default, which sends the message as it comes, as HTML.
    /// </summary>
    public bool FormatPlainText { get; set; }
}
