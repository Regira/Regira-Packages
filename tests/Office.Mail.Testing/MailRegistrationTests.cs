using System.Net;
using Microsoft.Extensions.DependencyInjection;
using Regira.Office.Mail.Abstractions;
using Regira.Office.Mail.MailGun;
using Regira.Office.Mail.MailKit;
using Regira.Office.Mail.Models;
using Regira.Office.Mail.SendGrid;
using MailGunRegistration = Regira.Office.Mail.MailGun.DependencyInjection.ServiceCollectionExtensions;
using MailKitRegistration = Regira.Office.Mail.MailKit.DependencyInjection.ServiceCollectionExtensions;
using SendGridRegistration = Regira.Office.Mail.SendGrid.DependencyInjection.ServiceCollectionExtensions;

namespace Office.Mail.Testing;

/// <summary>
/// The mail registrations and the requests the API senders send, read off a stub handler in place of the provider.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MailRegistrationTests
{
    private static readonly ServiceProviderOptions Validating = new() { ValidateScopes = true, ValidateOnBuild = true };

    private static IMessageObject CreateMessage() => new MessageObject
    {
        From = new MailAddress { Email = "sender@domain.com" },
        To = [new MailRecipient { Email = "recipient@domain.com" }],
        Subject = "Hello",
        Body = "<p>Hello</p>",
        IsHtml = true
    };

    public static IEnumerable<TestCaseData> Registrations()
    {
        yield return new TestCaseData((Action<IServiceCollection>)(s => SendGridRegistration.AddSendGrid(s, c => c.Key = "SG.key")), typeof(SendGridMailer)).SetArgDisplayNames("SendGrid");
        yield return new TestCaseData((Action<IServiceCollection>)(s => MailGunRegistration.AddMailGun(s, c =>
        {
            c.Api = "https://api.mailgun.net/v3";
            c.Key = "key";
            c.Domain = "example.com";
        })), typeof(MailGunMailer)).SetArgDisplayNames("MailGun");
        yield return new TestCaseData((Action<IServiceCollection>)(s => MailKitRegistration.AddMailKit(s, c => c.Host = "localhost")), typeof(MailKitMailer)).SetArgDisplayNames("MailKit");
    }

    [TestCaseSource(nameof(Registrations))]
    public void A_Sender_Resolves_From_The_Root_Provider(Action<IServiceCollection> register, Type expected)
    {
        var services = new ServiceCollection();
        register(services);
        using var sp = services.BuildServiceProvider(Validating);

        var mailer = sp.GetRequiredService<IMailService>();

        Assert.That(mailer, Is.TypeOf(expected));
    }

    [Test]
    public void The_Config_Is_Built_From_Configure_On_Every_Resolve()
    {
        var key = "SG.first";
        var services = new ServiceCollection();
        SendGridRegistration.AddSendGrid(services, c => c.Key = key);
        using var sp = services.BuildServiceProvider(Validating);

        using (var scope = sp.CreateScope())
        {
            Assert.That(scope.ServiceProvider.GetRequiredService<SendGridConfig>().Key, Is.EqualTo("SG.first"));
        }
        key = "SG.second";
        using (var scope = sp.CreateScope())
        {
            Assert.That(scope.ServiceProvider.GetRequiredService<SendGridConfig>().Key, Is.EqualTo("SG.second"));
        }
    }

    [Test]
    public async Task SendGrid_Sends_To_The_Api()
    {
        var handler = new ProviderStub(HttpStatusCode.Accepted);
        var mailer = new SendGridMailer(new SendGridConfig { Key = "SG.key" }) { Handler = handler };

        await mailer.Send(CreateMessage());

        Assert.That(handler.Requests, Is.EqualTo(new[] { "POST https://api.sendgrid.com/v3/mail/send Bearer SG.key" }));
    }

    [TestCase(null)]
    [TestCase("")]
    [TestCase(" ")]
    public void SendGrid_Refuses_A_Missing_Key_When_Built(string? key)
    {
        Assert.Throws<ArgumentNullException>(() => new SendGridMailer(new SendGridConfig { Key = key! }));
    }

    [Test]
    public async Task MailGun_Sends_To_The_Api()
    {
        var handler = new ProviderStub(HttpStatusCode.OK);
        var mailer = new MailGunMailer(new MailgunConfig { Api = "https://api.mailgun.net/v3", Key = "key", Domain = "example.com" }) { Handler = handler };

        await mailer.Send(CreateMessage());
        await mailer.Send(CreateMessage());

        Assert.That(handler.Requests, Has.Count.EqualTo(2));
        Assert.That(handler.Disposed, Is.False, "a send leaves the client open");
    }

    class ProviderStub(HttpStatusCode status) : HttpMessageHandler
    {
        public List<string> Requests { get; } = [];
        public bool Disposed { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Requests.Add($"{request.Method} {request.RequestUri} {request.Headers.Authorization}");
            return Task.FromResult(new HttpResponseMessage(status) { Content = new StringContent("""{ "message": "Queued" }""") });
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
