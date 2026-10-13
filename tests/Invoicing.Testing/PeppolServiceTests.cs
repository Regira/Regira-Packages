using System.Net;
using System.Text;
using System.Xml.Linq;
using Regira.Invoicing.ViaAdValvas.Config;
using Regira.Invoicing.ViaAdValvas.Models;
using Regira.Invoicing.ViaAdValvas.Services;
using Regira.Serializing.Newtonsoft.Json;

namespace Invoicing.Testing;

/// <summary>
/// The request <see cref="PeppolService"/> sends, read off a stub handler in place of the gateway.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class PeppolServiceTests
{
    private static readonly GatewaySettings Settings = new()
    {
        Uri = "https://gateway.test/api/",
        SenderID = "0208:0123456789",
        SenderName = "Regira",
        Token = "token",
        SecretKey = "secret"
    };

    private static XDocument Invoice()
    {
        XNamespace ubl = "urn:oasis:names:specification:ubl:schema:xsd:Invoice-2";
        XNamespace cac = "urn:oasis:names:specification:ubl:schema:xsd:CommonAggregateComponents-2";
        XNamespace cbc = "urn:oasis:names:specification:ubl:schema:xsd:CommonBasicComponents-2";
        return new XDocument(new XElement(ubl + "Invoice",
            new XElement(cac + "OrderReference", new XElement(cbc + "ID", "INV-001")),
            new XElement(cac + "AccountingCustomerParty",
                new XElement(cac + "Party",
                    new XElement(cbc + "EndpointID", new XAttribute("schemeID", "0208"), "0987654321")))));
    }

    [Test]
    public async Task Send_Posts_The_Document_To_The_Gateway()
    {
        var handler = new GatewayStub("""{ "Status": true }""");
        var service = new PeppolService(Settings, new JsonSerializer()) { Handler = handler };

        var response = await service.Send(Invoice());

        Assert.That(response.Success, Is.True);
        Assert.That(response.Reference, Is.EqualTo("INV-001"));
        Assert.That(handler.Request, Is.EqualTo("POST https://gateway.test/api/document?format=json ContentType=text/plain"));
        Assert.That(handler.Body, Does.Contain("\"receiverIdentification\":\"0208:0987654321\"").IgnoreCase);
    }

    [Test]
    public async Task A_Refused_Document_Throws_With_The_Gateway_Answer()
    {
        var handler = new GatewayStub("""{ "Status": false }""");
        var service = new PeppolService(Settings, new JsonSerializer()) { Handler = handler };

        var ex = await Assert.ThrowsAsync<PeppolResponseException>(() => service.Send(Invoice()));

        Assert.That(ex!.SerializedResponse, Does.Contain("false"));
    }

    class GatewayStub(string json) : HttpMessageHandler
    {
        public string? Request { get; private set; }
        public string? Body { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Request = $"{request.Method} {request.RequestUri} ContentType={string.Join(",", request.Headers.GetValues("ContentType"))}";
            Body = await request.Content!.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(HttpStatusCode.OK) { Content = new StringContent(json, Encoding.UTF8, "application/json") };
        }
    }
}
