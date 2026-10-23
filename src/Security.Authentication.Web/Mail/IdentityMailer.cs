using System.Net;
using System.Text.RegularExpressions;
using Microsoft.AspNetCore.Identity.UI.Services;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;

namespace Regira.Security.Authentication.Web.Mail;
public class IdentityMailer(IMailService mailer, IdentityMailerOptions options) : IEmailSender
{
    // Runs on encoded text, where every '&' starts a character reference: a link takes one only as "&amp;" (a '&' in
    // its query), so an encoded quote or angle bracket ends it, as white space does. It does not end on punctuation,
    // so ".../reset." links ".../reset". The scheme matches in any case.
    private static readonly Regex Link = new(@"https?://(?:[^\s&]|&amp;)*(?:[^\s&.,;:!?)]|&amp;)", RegexOptions.IgnoreCase);

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

    // Encoded before the anchors are added, so no character of the text can close the href or open a tag.
    // The line breaks come last: a <br> written earlier would end up inside the link before it.
    private static string FormatPlainText(string text)
    {
        var encoded = WebUtility.HtmlEncode(text);
        var linked = Link.Replace(encoded, m => $"<a href=\"{m.Value}\">{m.Value}</a>");
        return linked.ReplaceLineEndings("<br>");
    }
}