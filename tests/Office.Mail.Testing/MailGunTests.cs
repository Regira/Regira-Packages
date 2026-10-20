using Microsoft.Extensions.Configuration;
using Office.Mail.Testing.Abstractions;
using Regira.Office.Mail.MailGun;
using Regira.Office.Mail.Models;

namespace Office.Mail.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
[Category("Network")]
public class MailGunTests : MailerTestsBase
{
    private readonly string? _missingSecret;

    public MailGunTests()
    {
        var config = new ConfigurationBuilder()
            .AddUserSecrets<MailGunTests>()
            .Build();
        var api = config["Mail:MailGun:Api"];
        var key = config["Mail:MailGun:Key"];
        var domain = config["Mail:MailGun:Domain"];

        _missingSecret = new[] { ("Api", api), ("Key", key), ("Domain", domain) }
            .Where(x => string.IsNullOrWhiteSpace(x.Item2))
            .Select(x => $"Mail:MailGun:{x.Item1}")
            .FirstOrDefault();

        Mailer = new MailGunMailer(new MailgunConfig
        {
            Api = api!,
            Key = key!,
            Domain = domain!,
            // These send to a real address. o:testmode has Mailgun accept and log the call without
            // delivering, so the suite exercises the actual request — auth, multipart attachment, response
            // shape — without mailing anyone on every run.
            TestMode = true
        });
    }

    /// <summary>Skips rather than fails on a machine without the Mailgun user secrets.</summary>
    private void RequireCredentials()
    {
        if (_missingSecret != null)
        {
            Assert.Ignore($"Mailgun user secret '{_missingSecret}' is not configured");
        }
    }

    [Test]
    public override Task Send_Without_Attachment()
    {
        RequireCredentials();
        return base.Send_Without_Attachment();
    }
    [Test]
    public override Task Send_With_Attachment()
    {
        RequireCredentials();
        return base.Send_With_Attachment();
    }
    /// <summary>Mailgun takes a quoted display name holding a comma, and the <c>h:Reply-To</c> header.</summary>
    [Test]
    public async Task Send_With_Display_Names_And_ReplyTo()
    {
        RequireCredentials();
        var msg = new MessageObject
        {
            From = new MailAddress { Email = "bram@regira.com", DisplayName = "Regira Tests" },
            To = { new MailRecipient { Email = "bramverboven@hotmail.com", DisplayName = "Doe, Alice" } },
            ReplyTo = new MailAddress { Email = "bram@regira.com", DisplayName = "Regira \"Reply\" Desk" },
            Subject = $"Test from {Mailer.GetType().Name}",
            Body = "Testing display names and Reply-To..."
        };
        var response = await Mailer.Send(msg);

        Assert.That(response.Success, Is.True);
    }
}