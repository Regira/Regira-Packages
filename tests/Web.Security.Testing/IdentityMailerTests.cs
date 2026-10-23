using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.Models;
using Regira.Security.Authentication.Web.Mail;
using Shouldly;
using Xunit;

namespace Web.Security.Testing;

// IdentityMailer hands Identity's message to the registered IMailService. The account controllers write that message
// as plain text, which FormatPlainText turns into HTML without letting the text write markup of its own.
public class IdentityMailerTests
{
    [Fact]
    public async Task Default_SendsTheMessageAsItComes()
    {
        var mailService = new RecordingMailService();
        var mailer = new IdentityMailer(mailService, new IdentityMailerOptions { Sender = "no-reply@example.com" });

        await mailer.SendEmailAsync("user@example.com", "Welcome", "<p>Hello</p>\nhttps://app.example.com");

        mailService.Sent!.Body.ShouldBe("<p>Hello</p>\nhttps://app.example.com");
        mailService.Sent.IsHtml.ShouldBeTrue();
        mailService.Sent.Subject.ShouldBe("Welcome");
        mailService.Sent.To.Single().Email.ShouldBe("user@example.com");
    }

    [Fact]
    public async Task FormatPlainText_EncodesTheText_LinksTheUrl_AndKeepsTheLineBreaks()
    {
        // the shape of the confirmation mail UserControllerBase sends, with a user name Identity's defaults would refuse
        var body = "Welcome a&b<c>,\r\nPlease follow link below to confirm email\nhttps://app.example.com/confirm?token=e30%3D&x=1\n\nToken: e30%3D\n";

        var sent = await SendFormatted(body);

        sent.ShouldBe("Welcome a&amp;b&lt;c&gt;,<br>Please follow link below to confirm email<br>"
            + "<a href=\"https://app.example.com/confirm?token=e30%3D&amp;x=1\">https://app.example.com/confirm?token=e30%3D&amp;x=1</a>"
            + "<br><br>Token: e30%3D<br>");
    }

    [Fact]
    public async Task FormatPlainText_A_Link_Cannot_Leave_Its_Anchor()
    {
        var sent = await SendFormatted("https://app.example.com/\"onmouseover=\"alert(1)\"><script>alert(2)</script>");

        // the encoded quote ends the link, and the rest stays text
        sent.ShouldBe("<a href=\"https://app.example.com/\">https://app.example.com/</a>"
            + "&quot;onmouseover=&quot;alert(1)&quot;&gt;&lt;script&gt;alert(2)&lt;/script&gt;");
        sent.Count(c => c == '"').ShouldBe(2);
    }

    [Fact]
    public async Task FormatPlainText_A_Link_Ends_Before_Trailing_Punctuation()
    {
        var sent = await SendFormatted("Reset at https://app.example.com/reset. Or https://app.example.com/a, (https://app.example.com/v1.2/b); https://app.example.com/c!");

        sent.ShouldBe("Reset at <a href=\"https://app.example.com/reset\">https://app.example.com/reset</a>. "
            + "Or <a href=\"https://app.example.com/a\">https://app.example.com/a</a>, "
            + "(<a href=\"https://app.example.com/v1.2/b\">https://app.example.com/v1.2/b</a>); "
            + "<a href=\"https://app.example.com/c\">https://app.example.com/c</a>!");
    }

    [Fact]
    public async Task FormatPlainText_Links_Http_And_Https_Only_In_Any_Case()
    {
        var sent = await SendFormatted("javascript:alert(1) http://app.example.com HTTPS://APP.EXAMPLE.COM/RESET");

        sent.ShouldBe("javascript:alert(1) <a href=\"http://app.example.com\">http://app.example.com</a> "
            + "<a href=\"HTTPS://APP.EXAMPLE.COM/RESET\">HTTPS://APP.EXAMPLE.COM/RESET</a>");
    }

    private static async Task<string> SendFormatted(string body)
    {
        var mailService = new RecordingMailService();
        var mailer = new IdentityMailer(mailService, new IdentityMailerOptions { Sender = "no-reply@example.com", FormatPlainText = true });
        await mailer.SendEmailAsync("user@example.com", "Welcome", body);
        mailService.Sent!.IsHtml.ShouldBeTrue();
        return mailService.Sent.Body!;
    }

    private class RecordingMailService : IMailService
    {
        public IMessageObject? Sent { get; private set; }

        public Task<IMailResponse> Send(IMailAddress sender, IEnumerable<IMailRecipient> recipients, string? subject, string? message,
            bool isHtml = true, IEnumerable<Regira.IO.Abstractions.INamedFile>? attachments = null, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task<IMailResponse> Send(IMessageObject message, CancellationToken cancellationToken = default)
        {
            Sent = message;
            return Task.FromResult<IMailResponse>(new MailResponse { Success = true });
        }
    }
}
