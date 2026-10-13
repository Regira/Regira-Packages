using System.Net;
using System.Text;
using Regira.Invoicing.Payments.Models;
using Regira.Payments.Pom;
using Regira.Serializing.Newtonsoft.Json;

namespace Payments.Testing;

[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class PomTests
{
    private static readonly PomSettings Settings = new()
    {
        SenderContractNumber = "C-1",
        AuthApi = "https://pom.test/auth",
        CreatePaylinkApi = "https://pom.test/paylinks",
        PaylinkStatusApi = "https://pom.test/paylinks/{paymentId}"
    };

    [Test]
    public async Task Save_Sets_The_New_Id_On_The_Payment()
    {
        var handler = new PomStub();
        var service = new PaymentService(Settings, new JsonSerializer()) { Handler = handler };
        var payment = new Payment { Amount = 49.99m, Currency = "EUR" };

        await service.Save(payment);

        Assert.That(payment.Id, Is.EqualTo("POM-42"));
    }

    [Test]
    public async Task CreatePaylink_Returns_The_Pay_Link()
    {
        var handler = new PomStub();
        var service = new PaymentService(Settings, new JsonSerializer()) { Handler = handler };
        var payment = new Payment { Amount = 49.99m };

        var response = await service.CreatePaylink(payment);

        Assert.That(response.PomDocumentId, Is.EqualTo("POM-42"));
        Assert.That(response.Paylink, Is.EqualTo("https://pay.pom.test/POM-42"));
        Assert.That(payment.Id, Is.EqualTo("POM-42"));
    }

    [Test]
    public async Task Requests_Share_One_Handler_And_Authenticate_Once()
    {
        var handler = new PomStub();
        var service = new PaymentService(Settings, new JsonSerializer()) { Handler = handler };

        await service.Save(new Payment { Amount = 10 });
        await service.Details("POM-42");

        Assert.That(handler.Requests, Is.EqualTo(new[]
        {
            "POST https://pom.test/auth",
            "POST https://pom.test/paylinks token-1",
            "GET https://pom.test/paylinks/POM-42 token-1"
        }));
        Assert.That(handler.Disposed, Is.False);
    }

    [Test]
    public async Task An_Expired_Token_Is_Renewed_And_The_Request_Sent_Again()
    {
        var handler = new PomStub();
        var service = new PaymentService(Settings, new JsonSerializer()) { Handler = handler };
        await service.Details("POM-42");
        handler.Expired.Add("token-1");

        var payment = new Payment { Amount = 10 };
        await service.Save(payment);

        Assert.That(payment.Id, Is.EqualTo("POM-42"));
        Assert.That(handler.Requests, Is.EqualTo(new[]
        {
            "POST https://pom.test/auth",
            "GET https://pom.test/paylinks/POM-42 token-1",
            "POST https://pom.test/paylinks token-1 401",
            "POST https://pom.test/auth",
            "POST https://pom.test/paylinks token-2"
        }));
        Assert.That(handler.LastBody, Does.Contain("\"amount\":10"));
    }

    [Test]
    public async Task A_Token_Refused_Twice_Throws()
    {
        var handler = new PomStub { ExpireEveryToken = true };
        var service = new PaymentService(Settings, new JsonSerializer()) { Handler = handler };

        var ex = await Assert.ThrowsAsync<PomException>(() => service.Details("POM-42"));

        Assert.That(ex!.StatusCode, Is.EqualTo(401));
        Assert.That(handler.Requests.Count(r => r.StartsWith("POST https://pom.test/auth")), Is.EqualTo(2));
    }

    class PomStub : HttpMessageHandler
    {
        private int _tokens;
        public List<string> Requests { get; } = [];
        public HashSet<string> Expired { get; } = [];
        public bool ExpireEveryToken { get; init; }
        public string? LastBody { get; private set; }
        public bool Disposed { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var token = request.Headers.TryGetValues("X-Authentication", out var values) ? values.Single() : null;
            LastBody = request.Content == null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            if (token != null && (ExpireEveryToken || Expired.Contains(token)))
            {
                Requests.Add($"{request.Method} {request.RequestUri} {token} 401");
                return new HttpResponseMessage(HttpStatusCode.Unauthorized);
            }
            Requests.Add($"{request.Method} {request.RequestUri}{(token == null ? "" : " " + token)}");

            var json = request.RequestUri!.AbsolutePath switch
            {
                "/auth" => $$"""{ "authToken": "token-{{++_tokens}}" }""",
                "/paylinks" => """{ "pomDocumentId": "POM-42", "paylink": "https://pay.pom.test/POM-42" }""",
                _ => """{ "id": "POM-42", "amount": 10, "paymentStatus": "Paid" }"""
            };
            return new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json")
            };
        }

        protected override void Dispose(bool disposing)
        {
            Disposed = true;
            base.Dispose(disposing);
        }
    }
}
