using Regira.Licensing.Models;
using Regira.Office.Clients.Services;
using System.Net;

namespace Office.Clients.Testing;

/// <summary>
/// Unlike the other client tests this one does not call the hosted API: the endpoint's answer is canned, in
/// the shape the API produces (string enums, nulls dropped), so the test pins the client's deserialization
/// without depending on which key the test run happens to hold.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class LicenseStatusClientTests
{
    [Test]
    public async Task GetStatus_Reads_The_Api_Answer()
    {
        const string body = """
            {"state":"ExpiredInGrace","productCode":"regira.services","accepted":true,"customerId":"acme","tier":"paid",
             "products":["regira.services"],"issuedAt":"2026-03-02T12:00:00+00:00","expiresAt":"2026-08-30T12:00:00+00:00",
             "daysUntilExpiry":-3,"message":"The license key expired on 2026-08-30. Renew now at https://regira.com/licensing",
             "applied":"the key's own limits"}
            """;
        using var http = new HttpClient(new CannedHandler(body)) { BaseAddress = new Uri("https://office.example.test/") };
        var client = new LicenseStatusClient(http);

        var status = await client.GetStatus();

        Assert.That(status.State, Is.EqualTo(LicenseState.ExpiredInGrace));
        Assert.That(status.Accepted, Is.True);
        Assert.That(status.CustomerId, Is.EqualTo("acme"));
        Assert.That(status.ExpiresAt, Is.EqualTo(new DateTimeOffset(2026, 8, 30, 12, 0, 0, TimeSpan.Zero)));
        Assert.That(status.DaysUntilExpiry, Is.EqualTo(-3));
        Assert.That(status.Limits, Is.Null);
        Assert.That(status.Applied, Is.EqualTo("the key's own limits"));
    }

    [Test]
    public async Task GetStatus_Calls_The_Status_Path()
    {
        var handler = new CannedHandler("""{"state":"Missing","productCode":"regira.services","accepted":false,"message":"none"}""");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        await new LicenseStatusClient(http).GetStatus();

        Assert.That(handler.LastRequest?.RequestUri?.ToString(), Is.EqualTo("https://office.example.test/license/status"));
        Assert.That(handler.LastRequest?.Method, Is.EqualTo(HttpMethod.Get));
    }

    private sealed class CannedHandler(string json) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            });
        }
    }
}
