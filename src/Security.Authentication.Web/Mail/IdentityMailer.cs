using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Security.Authentication.Web.Mail;
public class IdentityMailer(IMailService mailer, IdentityMailerOptions options) : IEmailSender
{
    // Runs on the plain text: a link ends at white space, a quote or an angle bracket, and not on trailing punctuation,
    // so ".../reset." links ".../reset". The scheme matches in any case. The one group makes Split keep the links.
    private static readonly Regex Link = new(@"(https?://[^\s""'<>]*[^\s""'<>.,;:!?)])", RegexOptions.IgnoreCase);

    public Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var msg = new MessageObject
        {
            From = options.Sender,
            To = { new MailRecipient { Email = email } },
            Subject = subject,
            Body = options.FormatPlainText ? FormatPlainText(htmlMessage) : htmlMessage
        };
        return mailer.Send(msg, CancellationToken.None);
    }

    // Every piece is encoded, a link inside its href too, so no character of the text can close the href or open a tag,
    // and a character the encoder writes as a reference (ü as &#252;) stays inside its link. The line breaks come last:
    // a link holds no white space, so none of them lands inside one.
    private static string FormatPlainText(string text)
        => string.Concat(Link.Split(text).Select((part, i) =>
        {
            var encoded = WebUtility.HtmlEncode(part);
            return i % 2 == 0 ? encoded : $"<a href=\"{encoded}\">{encoded}</a>";
        })).ReplaceLineEndings("<br>");
}