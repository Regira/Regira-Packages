using System.Net;
using Regira.Office.Clients.Services;
using Regira.Office.Models;
using Regira.Office.PDF.Models;

namespace Office.Clients.Testing;

/// <summary>
/// The Office API's <c>pdf/from-images</c> takes the images only, so <see cref="PdfClient"/> refuses a layout it would
/// drop. The answers are canned, so no API is called.
/// </summary>
[TestFixture]
[Parallelizable(ParallelScope.Self)]
public class PdfClientRequestTests
{
    [Test]
    public async Task ImagesToPdf_Refuses_A_Layout_The_Api_Drops()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };
        var client = new PdfClient(http);

        await Assert.ThrowsAsync<NotSupportedException>(() => client.ImagesToPdf(new ImagesInput { Orientation = PageOrientation.Landscape }));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.ImagesToPdf(new ImagesInput { Format = PageSize.A3 }));
        await Assert.ThrowsAsync<NotSupportedException>(() => client.ImagesToPdf(new ImagesInput { Margins = 0f }));
        Assert.That(handler.Calls, Is.Zero, "refused before anything is posted");
    }

    // the defaults at any DPI are the API's own layout
    [TestCase(96)]
    [TestCase(300)]
    public async Task ImagesToPdf_Posts_The_Default_Layout(int dpi)
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        await new PdfClient(http).ImagesToPdf(new ImagesInput(dpi) { Images = [[1, 2, 3]] });

        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    // a DPI set in an initializer leaves the margins at their 96-DPI default: the caller set no layout either
    [Test]
    public async Task ImagesToPdf_Posts_An_Input_Whose_Dpi_Was_Set_In_An_Initializer()
    {
        var handler = new CountingHandler();
        using var http = new HttpClient(handler) { BaseAddress = new Uri("https://office.example.test/") };

        await new PdfClient(http).ImagesToPdf(new ImagesInput { DPI = 300, Images = [[1, 2, 3]] });

        Assert.That(handler.Calls, Is.EqualTo(1));
    }

    private sealed class CountingHandler : HttpMessageHandler
    {
        public int Calls { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK) { Content = new ByteArrayContent("%PDF-1.7"u8.ToArray()) });
        }
    }
}
