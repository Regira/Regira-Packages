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
        // the serializer injected here writes camelCase; the gateway expects the declared names
        Assert.That(handler.Body, Does.Contain("\"ReceiverIdentification\":\"0208:0987654321\""));
        Assert.That(handler.Body, Does.Not.Contain("\"receiverIdentification\""));
    }

    // The body is what Newtonsoft's defaults write for the request, as the gateway has always received it: declared
    // names, nulls included, and a date's "+" offset and accented letters unescaped.
    [Test]
    public void The_Request_Body_Is_What_Newtonsoft_Defaults_Write()
    {
        var request = new UblDocumentRequest
        {
            Channel = "Peppol",
            DateTime = "2026-10-18T10:15:30.1234567+02:00",
            MessageReference = "INV-001",
            ReceiverIdentification = "0208:0987654321",
            Seal = "q+Z/9w==",
            SenderIdentification = "0208:0123456789",
            SenderName = "Garage Dupré & Fils <Gent>",
            Token = "token",
            DocumentType = "Invoice EN-UBL CIUS PEPPOL",
            Document = [60, 63, 120, 255]
        };

        Assert.That(PeppolService.SerializeRequest(request), Is.EqualTo(Newtonsoft.Json.JsonConvert.SerializeObject(request)));
    }

    [Test]
    public async Task Send_Posts_The_Body_Newtonsoft_Defaults_Write_Whatever_Serializer_Is_Injected()
    {
        var handler = new GatewayStub("""{ "Status": true }""");
        var settings = new GatewaySettings { Uri = Settings.Uri, SenderID = Settings.SenderID, SenderName = "Garage Dupré", Token = "token", SecretKey = "secret" };
        var service = new PeppolService(settings, new JsonSerializer()) { Handler = handler };

        await service.Send(Invoice());

        var sent = Newtonsoft.Json.JsonConvert.DeserializeObject<UblDocumentRequest>(handler.Body!)!;
        Assert.That(handler.Body, Is.EqualTo(Newtonsoft.Json.JsonConvert.SerializeObject(sent)));
        Assert.That(sent.SenderName, Is.EqualTo("Garage Dupré"));
        Assert.That(handler.Body, Does.Contain("\"ProcessType\":null"));
    }

    // Characters the two write with another escape (an emoji, a no-break space, DEL, a byte order mark) still read back
    // as the same text.
    [Test]
    public void Characters_Escaped_Otherwise_Read_Back_The_Same()
    {
        var name = "Caf" + char.ConvertFromUtf32(0x1F600) + (char)0x00A0 + (char)0x007F + (char)0xFEFF;
        var request = new UblDocumentRequest { SenderName = name, Document = [] };

        var read = Newtonsoft.Json.JsonConvert.DeserializeObject<UblDocumentRequest>(PeppolService.SerializeRequest(request))!;

        Assert.That(read.SenderName, Is.EqualTo(name));
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
