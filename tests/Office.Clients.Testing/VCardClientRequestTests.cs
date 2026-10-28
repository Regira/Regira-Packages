using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Regira.Office.Clients.Services;
using Regira.Office.VCards.Abstractions;
using Regira.Office.VCards.Exceptions;
using Regira.Office.VCards.Models;

namespace Office.Clients.Testing;

/// <summary>
/// What <see cref="VCardClient"/> sends the Office API's <c>vcard/read</c> and <c>vcard/write</c>, and how it reads their
/// answers: <c>read</c> takes the .vcf as a multipart <c>vcfFile</c> and answers the contacts as JSON, or a 400 with a
/// message for content it cannot read; <c>write</c> takes the contacts as JSON and answers the .vcf as text. The answers
/// are canned, so no API is called.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class VCardClientRequestTests
{
    private const string Vcf = "BEGIN:VCARD\r\nVERSION:3.0\r\nN:Janssens;Zoë;;;\r\nFN:Zoë Janssens\r\nEND:VCARD\r\n";

    private static readonly JsonSerializerOptions ApiJson = new()
    {
        PropertyNameCaseInsensitive = true,
        Converters = { new JsonStringEnumConverter() }
    };

    [Test]
    public async Task ReadMany_Posts_The_Content_As_Utf8_And_Reads_Every_Contact()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, """[{"formattedName":"Zoë Janssens"},{"formattedName":"Jan Peeters"}]""", "application/json");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        var cards = (await new VCardClient(http).ReadMany(Vcf)).ToList();

        var request = handler.Requests.Single();
        Assert.That(request.PathAndQuery, Is.EqualTo("/vcard/read"));
        Assert.That(request.FilePart, Is.EqualTo("vcfFile"));
        Assert.That(request.Bytes, Is.EqualTo(Encoding.UTF8.GetBytes(Vcf)), "UTF-8 without a byte order mark");
        Assert.That(cards.Select(card => card.FormattedName), Is.EqualTo(new[] { "Zoë Janssens", "Jan Peeters" }));
    }

    [Test]
    public async Task A_400_Is_An_Invalid_Card_With_The_Apis_Message()
    {
        var handler = new CannedHandler(HttpStatusCode.BadRequest, "\"No contact found\"", "application/json");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };
        var client = new VCardClient(http);

        var ex = await Assert.ThrowsAsync<InvalidCardException>(() => client.ReadMany("This is not a vCard."));
        await Assert.ThrowsAsync<InvalidCardException>(() => client.Read("This is not a vCard."));

        Assert.That(ex!.Message, Is.EqualTo("No contact found"));
        Assert.That(ex.CardContent, Is.EqualTo("This is not a vCard."));
    }

    // an answer that says nothing about the card is the API's or the licence's failure, not the content's
    [TestCase(HttpStatusCode.InternalServerError)]
    [TestCase(HttpStatusCode.Unauthorized)]
    [TestCase(HttpStatusCode.TooManyRequests)]
    public async Task Another_Failure_Is_An_Http_Error(HttpStatusCode status)
    {
        var handler = new CannedHandler(status, "failed", "text/plain");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };
        var client = new VCardClient(http);

        await Assert.ThrowsAsync<HttpRequestException>(() => client.ReadMany(Vcf));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.Read(Vcf));
        await Assert.ThrowsAsync<HttpRequestException>(() => client.Write(new VCard { FormattedName = "Zoë Janssens" }));
    }

    [Test]
    public async Task Write_Posts_The_Contacts_As_Json_In_The_Asked_Version()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, Vcf, "text/vcard");
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        var vcf = await new VCardClient(http).Write([new VCard { FormattedName = "Zoë Janssens" }], VCardVersion.V4_0);

        var request = handler.Requests.Single();
        Assert.That(request.PathAndQuery, Is.EqualTo("/vcard/write?version=V4_0"));
        var sent = JsonSerializer.Deserialize<List<VCard>>(request.Bytes, ApiJson)!;
        Assert.That(sent.Single().FormattedName, Is.EqualTo("Zoë Janssens"));
        Assert.That(vcf, Is.EqualTo(Vcf));
    }

    // the caller's token reaches the request: cancelled while the API has not answered, every call stops
    [Test]
    public async Task A_Cancelled_Call_Stops_While_The_Api_Has_Not_Answered()
    {
        var handler = new CannedHandler(HttpStatusCode.OK, "[]", "application/json") { Hold = true };
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/"), Timeout = Timeout.InfiniteTimeSpan };
        var client = new VCardClient(http);

        foreach (var call in new Func<CancellationToken, Task>[]
                 {
                     token => client.ReadMany(Vcf, token),
                     token => client.Read(Vcf, token),
                     token => client.Write(new VCard(), VCardVersion.V3_0, token),
                     token => client.Write([new VCard()], VCardVersion.V3_0, token)
                 })
        {
            using var cancel = new CancellationTokenSource();
            var started = handler.NextRequest();
            var pending = call(cancel.Token);
            await started;
            cancel.Cancel();

            var ended = await Task.WhenAny(pending, Task.Delay(TimeSpan.FromSeconds(10)));

            Assert.That(ended, Is.SameAs(pending), "the call goes on after its token was cancelled");
            await Assert.ThatAsync(() => pending, Throws.InstanceOf<OperationCanceledException>());
        }
    }

    private sealed record RecordedRequest(string PathAndQuery, string? FilePart, byte[] Bytes);

    private sealed class CannedHandler(HttpStatusCode status, string body, string mediaType) : HttpMessageHandler
    {
        private TaskCompletionSource _next = new(TaskCreationOptions.RunContinuationsAsynchronously);
        public List<RecordedRequest> Requests { get; } = [];
        // holds each request until its token is cancelled
        public bool Hold { get; init; }

        public Task NextRequest()
        {
            _next = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
            return _next.Task;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            string? filePart = null;
            byte[] bytes = [];
            if (request.Content is MultipartFormDataContent form)
            {
                var part = form.Single();
                filePart = part.Headers.ContentDisposition!.Name!.Trim('"');
                bytes = await part.ReadAsByteArrayAsync(CancellationToken.None);
            }
            else if (request.Content != null)
            {
                bytes = await request.Content.ReadAsByteArrayAsync(CancellationToken.None);
            }
            Requests.Add(new RecordedRequest(request.RequestUri!.PathAndQuery, filePart, bytes));
            _next.TrySetResult();
            if (Hold)
            {
                await Task.Delay(Timeout.Infinite, cancellationToken);
            }

            return new HttpResponseMessage(status) { Content = new StringContent(body, Encoding.UTF8, mediaType) };
        }
    }
}
