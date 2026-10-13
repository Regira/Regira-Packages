using System.Net;
using System.Text;
using Regira.Invoicing.Payments.Models;
using Regira.Payments.Mollie.Config;
using Regira.Payments.Mollie.Services;

namespace Payments.Testing;

/// <summary>
/// The requests <see cref="PaymentService"/> sends, read off a stub handler in place of Mollie's API.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class MollieClientTests
{
    private static readonly MollieConfig Config = new() { Key = "test_regira", RedirectFactory = _ => "https://shop.test/paid" };

    [Test]
    public async Task Save_Sends_The_Payment()
    {
        var handler = new MollieStub();
        var service = new PaymentService(Config) { Handler = handler };
        var payment = new Payment { Amount = 10, Currency = "EUR", Description = "Order 1" };

        var response = await service.Save(payment);

        Assert.That(payment.Id, Is.EqualTo("tr_42"));
        Assert.That(response.CheckoutUrl, Is.EqualTo("https://www.mollie.com/checkout/tr_42"));
        Assert.That(handler.Requests.Single().Line, Is.EqualTo("POST https://api.mollie.com/v2/payments Bearer test_regira"));
    }

    [Test]
    public async Task Save_With_An_Idempotency_Key_Repeats_It()
    {
        var handler = new MollieStub();
        var service = new PaymentService(Config) { Handler = handler };

        await service.Save(new Payment { Amount = 10 }, "order-1");
        await service.Save(new Payment { Amount = 10 }, "order-1");
        await service.Save(new Payment { Amount = 10 });
        await service.Save(new Payment { Amount = 10 });

        var keys = handler.Requests.Select(r => r.IdempotencyKey).ToArray();
        Assert.That(keys[0], Is.EqualTo("order-1"));
        Assert.That(keys[1], Is.EqualTo("order-1"));
        Assert.That(keys[2], Is.Not.EqualTo("order-1"), "a save without a key gets a key of its own");
        Assert.That(keys[3], Is.Not.EqualTo(keys[2]));
    }

    [TestCase("")]
    [TestCase(" ")]
    public async Task Save_Refuses_An_Empty_Idempotency_Key(string key)
    {
        var service = new PaymentService(Config) { Handler = new MollieStub() };

        await Assert.ThrowsAsync<ArgumentException>(() => service.Save(new Payment { Amount = 10 }, key));
    }

    class MollieStub : HttpMessageHandler
    {
        public List<(string Line, string? IdempotencyKey)> Requests { get; } = [];

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var key = request.Headers.TryGetValues("Idempotency-Key", out var values) ? values.Single() : null;
            Requests.Add(($"{request.Method} {request.RequestUri} {request.Headers.Authorization}", key));
            const string json = """
                {
                  "resource": "payment",
                  "id": "tr_42",
                  "mode": "test",
                  "createdAt": "2026-10-13T10:00:00+00:00",
                  "amount": { "value": "10.00", "currency": "EUR" },
                  "description": "Order 1",
                  "status": "open",
                  "_links": {
                    "self": { "href": "https://api.mollie.com/v2/payments/tr_42", "type": "application/hal+json" },
                    "checkout": { "href": "https://www.mollie.com/checkout/tr_42", "type": "text/html" }
                  }
                }
                """;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.Created)
            {
                Content = new StringContent(json, Encoding.UTF8, "application/hal+json")
            });
        }
    }
}
