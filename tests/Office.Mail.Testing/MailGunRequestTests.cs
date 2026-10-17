using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.MailGun;
using Regira.Office.Mail.Models;
using Regira.Office.Mail.Web;
using Regira.Serializing.Abstractions;
using Regira.Serializing.Newtonsoft.Json;
using RestSharp;

namespace Office.Mail.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MailGunRequestTests
{
    private readonly ISerializer _serializer = new JsonSerializer();
    private readonly MailGunMailer _mailer = new(new MailgunConfig { Api = "https://api.mailgun.net/v3", Key = "key", Domain = "example.com" });

    [Test]
    public void Request_Carries_Display_Names_And_ReplyTo()
    {
        var input = _serializer.Deserialize<MailInput>(MailConstants.EXTENDED_INPUT)!;
        input.To!.Add(new MailInput.Recipient { Email = "cc@domain.com", DisplayName = "Copy", RecipientType = RecipientTypes.Cc });
        input.To.Add(new MailInput.Recipient { Email = "bcc@domain.com", RecipientType = RecipientTypes.Bcc });
        var msg = input.ToMessageObject();

        var request = _mailer.BuildRequest(msg);

        Assert.That(Values(request, "from"), Is.EqualTo(new[] { "My Name <sender@domain.com>" }));
        Assert.That(Values(request, "h:Reply-To"), Is.EqualTo(new[] { "Reply To <reply_to_me@domain.com>" }));
        Assert.That(Values(request, "to"), Is.EqualTo(new[]
        {
            "First Recipient <recipient1@domain.com>",
            "Second Recipient <recipient2@domain.com>",
            "recipient3@domain.com"
        }));
        Assert.That(Values(request, "cc"), Is.EqualTo(new[] { "Copy <cc@domain.com>" }));
        Assert.That(Values(request, "bcc"), Is.EqualTo(new[] { "bcc@domain.com" }));
    }

    [Test]
    public void Request_Without_ReplyTo_Sends_No_ReplyTo_Header()
    {
        var msg = _serializer.Deserialize<MailInput>(MailConstants.SIMPLE_INPUT)!.ToMessageObject();

        var request = _mailer.BuildRequest(msg);

        Assert.That(Values(request, "from"), Is.EqualTo(new[] { "sender@domain.com" }));
        Assert.That(Values(request, "h:Reply-To"), Is.Empty);
    }

    [TestCase(null, "alice@example.com")]
    [TestCase("", "alice@example.com")]
    [TestCase("  ", "alice@example.com")]
    [TestCase("Alice", "Alice <alice@example.com>")]
    [TestCase("Doe, Alice", "\"Doe, Alice\" <alice@example.com>")]
    [TestCase("Alice \"Al\" Doe", "\"Alice \\\"Al\\\" Doe\" <alice@example.com>")]
    [TestCase("Alice\r\nBcc: x@y.com", "\"Alice Bcc: x@y.com\" <alice@example.com>")]
    public void FormatAddress(string? displayName, string expected)
    {
        IMailAddress address = new MailAddress { Email = "alice@example.com", DisplayName = displayName };
        Assert.That(MailGunMailer.FormatAddress(address), Is.EqualTo(expected));
    }

    private static string?[] Values(RestRequest request, string name)
        => request.Parameters.Where(p => p.Name == name).Select(p => p.Value?.ToString()).ToArray();
}
