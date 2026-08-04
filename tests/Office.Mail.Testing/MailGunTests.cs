using Microsoft.Extensions.Configuration;
using Office.Mail.Testing.Abstractions;
using Regira.Office.Mail.MailGun;

namespace Office.Mail.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
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
}