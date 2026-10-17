using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using MailKit.Net.Smtp;
using MailKit.Security;
using Office.Mail.Testing.Abstractions;
using Regira.IO.Models;
using Regira.Office.Mail.Exceptions;
using Regira.Office.Mail.MailKit;
using Regira.Office.Mail.Models;

namespace Office.Mail.Testing;

/// <summary>
/// Sends through <see cref="MailKitMailer"/> to a Mailpit server in a container and reads back what arrived
/// through Mailpit's HTTP API. Mailpit requires SMTP AUTH and accepts recipients at a few domains only, so the
/// refusals are real server replies.
/// </summary>
/// <remarks>
/// Gated like <c>tests\DAL.PostgreSQL.Testing</c>: the fixture is skipped unless
/// <c>REGIRA_PROVIDER_TESTS=containers</c> is set, and skips rather than fails when Docker is unavailable.
/// </remarks>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
[Category("Containers")]
public class MailKitTests : MailerTestsBase
{
    public const string EnvVar = "REGIRA_PROVIDER_TESTS";
    public const string EnableValue = "containers";

    private const ushort SmtpPort = 1025;
    private const ushort ApiPort = 8025;
    private const string UserName = "mailer";
    private const string Password = "secret";

    private IContainer? _container;
    private HttpClient? _api;

    [OneTimeSetUp]
    public async Task OneTimeSetUp()
    {
        if (!string.Equals(Environment.GetEnvironmentVariable(EnvVar), EnableValue, StringComparison.OrdinalIgnoreCase))
        {
            Assert.Ignore($"Skipped: set {EnvVar}={EnableValue} to run container-backed tests.");
        }

        try
        {
            _container = new ContainerBuilder("axllent/mailpit:v1.31.4")
                .WithPortBinding(SmtpPort, true)
                .WithPortBinding(ApiPort, true)
                .WithEnvironment("MP_SMTP_AUTH", $"{UserName}:{Password}")
                .WithEnvironment("MP_SMTP_AUTH_ALLOW_INSECURE", "true")
                .WithEnvironment("MP_SMTP_ALLOWED_RECIPIENTS", @"@(example\.com|myshop\.com|hotmail\.com)$")
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(ApiPort).ForPath("/api/v1/info")))
                .Build();
            await _container.StartAsync();
        }
        catch (Exception ex)
        {
            Assert.Ignore($"Mailpit container could not start (Docker unavailable?): {ex.Message}");
        }

        _api = new HttpClient { BaseAddress = new Uri($"http://{_container!.Hostname}:{_container.GetMappedPublicPort(ApiPort)}") };
        Mailer = new MailKitMailer(Config());
    }

    [OneTimeTearDown]
    public async Task OneTimeTearDown()
    {
        _api?.Dispose();
        if (_container != null)
        {
            await _container.DisposeAsync();
        }
    }

    private MailKitConfig Config(string password = Password, SecureSocketOptions security = SecureSocketOptions.None) => new()
    {
        Host = _container!.Hostname,
        Port = _container.GetMappedPublicPort(SmtpPort),
        Security = security,
        UserName = UserName,
        Password = password
    };

    [Test]
    public override Task Send_Without_Attachment() => base.Send_Without_Attachment();
    [Test]
    public override Task Send_With_Attachment() => base.Send_With_Attachment();

    [Test]
    public async Task Delivers_Addresses_By_Type_And_Attachment()
    {
        var subject = $"Order confirmation {Guid.NewGuid()}";
        var msg = new MessageObject
        {
            From = new MailAddress { Email = "orders@myshop.com", DisplayName = "MyShop" },
            ReplyTo = new MailAddress { Email = "support@myshop.com", DisplayName = "Support" },
            To =
            {
                new MailRecipient { Email = "alice@example.com", DisplayName = "Alice" },
                new MailRecipient { Email = "sales@myshop.com", RecipientType = RecipientTypes.Cc },
                new MailRecipient { Email = "audit@myshop.com", RecipientType = RecipientTypes.Bcc }
            },
            Subject = subject,
            Body = "<p>Thanks for your order</p>",
            Attachments = [new BinaryFileItem { FileName = "file1.pdf", Bytes = await File.ReadAllBytesAsync(Path.Combine(Assets, "file1.pdf")) }]
        };

        var response = await Mailer.Send(msg);

        Assert.That(response.Success, Is.True);
        Assert.That(response.Content, Does.StartWith("2.0.0"));
        var received = (await FindBySubject(subject)).Single();
        Assert.That(Address(received.GetProperty("From")), Is.EqualTo("MyShop <orders@myshop.com>"));
        Assert.That(Addresses(received, "To"), Is.EqualTo(new[] { "Alice <alice@example.com>" }));
        Assert.That(Addresses(received, "Cc"), Is.EqualTo(new[] { "sales@myshop.com" }));
        Assert.That(Addresses(received, "Bcc"), Is.EqualTo(new[] { "audit@myshop.com" }));
        Assert.That(Addresses(received, "ReplyTo"), Is.EqualTo(new[] { "Support <support@myshop.com>" }));
        Assert.That(received.GetProperty("Attachments").GetInt32(), Is.EqualTo(1));
    }

    [Test]
    public async Task Refused_Recipient_Delivers_Nothing()
    {
        var subject = $"Refused {Guid.NewGuid()}";
        var msg = new MessageObject
        {
            From = (MailAddress)"orders@myshop.com",
            To = { (MailRecipient)"alice@example.com", (MailRecipient)"bob@refused.test" },
            Subject = subject,
            Body = "Testing..."
        };

        var ex = (await Assert.ThrowsAsync<MailException>(() => Mailer.Send(msg)))!;

        Assert.That(ex.InnerException, Is.TypeOf<SmtpCommandException>());
        Assert.That(ex.ResponseContent, Does.Contain("bob@refused.test"));
        Assert.That(await FindBySubject(subject), Is.Empty);
    }

    [Test]
    public async Task Wrong_Password_Throws_MailException()
    {
        var mailer = new MailKitMailer(Config(password: "wrong"));
        var msg = new MessageObject { From = (MailAddress)"orders@myshop.com", To = { (MailRecipient)"alice@example.com" }, Body = "Testing..." };

        var ex = (await Assert.ThrowsAsync<MailException>(() => mailer.Send(msg)))!;

        Assert.That(ex.InnerException, Is.TypeOf<AuthenticationException>());
        Assert.That(ex.ResponseContent, Does.Contain("535"));
    }

    [Test]
    public async Task Default_Security_Refuses_A_Server_Without_Tls()
    {
        // MailKitConfig's default, StartTls, fails instead of authenticating in clear text
        var mailer = new MailKitMailer(Config(security: new MailKitConfig().Security));
        var msg = new MessageObject { From = (MailAddress)"orders@myshop.com", To = { (MailRecipient)"alice@example.com" }, Body = "Testing..." };

        await Assert.ThrowsAsync<NotSupportedException>(() => mailer.Send(msg));
    }

    private async Task<JsonElement[]> FindBySubject(string subject)
    {
        using var doc = JsonDocument.Parse(await _api!.GetStringAsync("/api/v1/messages?limit=500"));
        return doc.RootElement.GetProperty("messages").EnumerateArray()
            .Where(x => x.GetProperty("Subject").GetString() == subject)
            .Select(x => x.Clone())
            .ToArray();
    }
    private static string[] Addresses(JsonElement message, string property)
        => message.GetProperty(property).EnumerateArray().Select(Address).ToArray();
    private static string Address(JsonElement address)
    {
        var name = address.GetProperty("Name").GetString();
        var email = address.GetProperty("Address").GetString();
        return string.IsNullOrEmpty(name) ? email! : $"{name} <{email}>";
    }
}
