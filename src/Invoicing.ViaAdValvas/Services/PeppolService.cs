using Regira.Invoicing.ViaAdValvas.Config;
using Regira.Invoicing.Peppol;
using Regira.Invoicing.ViaAdValvas.Models;
using Regira.Serializing.Abstractions;
using System.Globalization;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Xml.Linq;

namespace Regira.Invoicing.ViaAdValvas.Services;

public class PeppolService(GatewaySettings settings, ISerializer serializer)
{
    // every instance shares one connection pool, recycled so a change to the gateway's DNS reaches a long-running process;
    // no cookies, so nothing one instance receives rides on another's requests
    private static readonly SocketsHttpHandler SharedHandler = new() { PooledConnectionLifetime = TimeSpan.FromMinutes(2), UseCookies = false };
    // the handler every client goes through; a test points it at a stub
    internal HttpMessageHandler Handler { get; init; } = SharedHandler;

    // The gateway expects the request's declared (PascalCase) property names, nulls included, so the request is written
    // with these options whatever ISerializer the service was given; that one only reads the gateway's answer. The
    // relaxed encoder leaves a date's "+" offset and accented letters as they are: the body goes to an API, never into
    // a page.
    private static readonly JsonSerializerOptions RequestJson = new() { Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping };


    public async Task<UblDocumentResponse> Send(XDocument ublDoc)
    {
        var sendUrl = new Uri(new Uri(settings.Uri), "document?format=json");

        var request = CreateRequest(ublDoc);
        var serializedRequest = SerializeRequest(request);
        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, sendUrl);
        // a custom header every release has sent; the Content-Type itself comes from the StringContent
        httpRequest.Headers.TryAddWithoutValidation("ContentType", "text/plain");
        httpRequest.Content = new StringContent(serializedRequest);
        using var httpResponse = await new HttpClient(Handler, disposeHandler: false).SendAsync(httpRequest);
        if (!httpResponse.IsSuccessStatusCode)
        {
            throw new PeppolRequestException("Posting document failed")
            {
                ServiceStatusCode = (int)httpResponse.StatusCode,
                ResponseContent = await httpResponse.Content.ReadAsStringAsync()
            };
        }

        var serializedResponse = await httpResponse.Content.ReadAsStringAsync();

        var response = serializer.Deserialize<ValidateResponse>(serializedResponse);
        if (response?.Status != true)
        {
            throw new PeppolResponseException("Sending document failed")
            {
                ServiceStatusCode = (int)httpResponse.StatusCode,
                SerializedResponse = serializedResponse
            };
        }
        return CreateResponse(request.MessageReference, response.Status);
    }


    /// <summary>The request body: the declared (PascalCase) property names the gateway expects, nulls included.</summary>
    internal static string SerializeRequest(UblDocumentRequest request) => JsonSerializer.Serialize(request, RequestJson);

    protected UblDocumentRequest CreateRequest(XDocument doc)
    {
        var referenceId = doc.GetReferenceId();
        var receiverId = $"{doc.GetRecipientSchemeId()}:{doc.GetRecipientId()}";
        var date = DateTime.Now;
        var seal = Helpers.SealUtility.Generate(settings.SecretKey, settings.Token, date, settings.SenderID, referenceId);

        var isCreditNote = doc.Root?.Name.LocalName == "CreditNote";

        using var ms = new MemoryStream();
        doc.Save(ms);
        var bytes = ms.ToArray();

        var request = new UblDocumentRequest
        {
            Channel = "Peppol",
            DateTime = date.ToString("yyyy-MM-ddTHH:mm:ss.fffffffzzz", CultureInfo.InvariantCulture),
            MessageReference = referenceId,
            ReceiverIdentification = receiverId,
            Seal = seal,
            SenderIdentification = settings.SenderID,
            SenderName = settings.SenderName,
            Token = settings.Token,
            DocumentType = $"{(isCreditNote ? "CreditNote" : "Invoice")} EN-UBL CIUS PEPPOL",
            Document = bytes.Select(b => (int)b).ToArray()
        };

        return request;
    }
    protected UblDocumentResponse CreateResponse(string? reference, bool isSuccess)
    {
        return new UblDocumentResponse
        {
            Reference = reference,
            Success = isSuccess
        };
    }
}