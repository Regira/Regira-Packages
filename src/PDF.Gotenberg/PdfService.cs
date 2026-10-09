using System.Net;
using System.Text;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.Office.MimeTypes;
using Regira.Office.PDF.Abstractions;
using Regira.Office.PDF.Internal;
using Regira.Office.PDF.Models;

namespace Regira.Office.PDF.Gotenberg;

/// <summary>
/// Renders HTML to PDF through the Chromium route of a <see href="https://gotenberg.dev">Gotenberg</see> server, so
/// no browser runs inside the application. Every <see cref="HtmlInput"/> setting is applied, as on PDF.MsPlaywright:
/// the page size and orientation, the margins, and the header and footer, which Chromium draws in a band inside the
/// top and bottom margin.
/// <para>
/// Gotenberg's other Chromium options keep their defaults: backgrounds are not printed, the <c>print</c> media type
/// applies, and <see cref="PdfInputBase.Format"/> wins over a CSS <c>@page</c> size. The HTML is rendered inside the
/// Gotenberg container, so it uses the fonts installed there, and an absolute URL in it is fetched from there.
/// </para>
/// </summary>
/// <param name="client">
/// An <see cref="HttpClient"/> whose <see cref="HttpClient.BaseAddress"/> points at the Gotenberg server
/// (with a trailing slash). <c>AddGotenbergPdf</c> configures it from <see cref="GotenbergPdfConfig"/>.
/// </param>
public class PdfService(HttpClient client) : IHtmlToPdfService
{
    internal const string ConvertRoute = "forms/chromium/convert/html";

    public async Task<IMemoryFile> Create(HtmlInput template, CancellationToken cancellationToken = default)
    {
        var layout = ChromiumPdfLayout.From(template);

        using var content = new MultipartFormDataContent();
        // Gotenberg renders the file named index.html; it reads a header and footer only as whole documents
        content.Add(Html(template.HtmlContent ?? string.Empty), "files", "index.html");
        if (layout.HeaderTemplate != null)
        {
            content.Add(Html(Document(layout.HeaderTemplate)), "files", "header.html");
        }
        if (layout.FooterTemplate != null)
        {
            content.Add(Html(Document(layout.FooterTemplate)), "files", "footer.html");
        }
        content.Add(new StringContent(layout.Width), "paperWidth");
        content.Add(new StringContent(layout.Height), "paperHeight");
        content.Add(new StringContent(layout.MarginTop), "marginTop");
        content.Add(new StringContent(layout.MarginRight), "marginRight");
        content.Add(new StringContent(layout.MarginBottom), "marginBottom");
        content.Add(new StringContent(layout.MarginLeft), "marginLeft");

        using var response = await client.PostAsync(ConvertRoute, content, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            var hint = response.StatusCode == HttpStatusCode.ServiceUnavailable
                ? " Gotenberg answers 503 when a conversion exceeds its --api-timeout (30 seconds by default)."
                : string.Empty;
            throw new HttpRequestException(
                $"{response.RequestMessage?.RequestUri} returned {(int)response.StatusCode} {response.ReasonPhrase}: {body}{hint}",
                null,
                response.StatusCode);
        }

        var bytes = await response.Content.ReadAsByteArrayAsync(cancellationToken);
        return bytes.ToMemoryFile(ContentTypes.PDF);
    }


    private static ByteArrayContent Html(string html)
        => new(Encoding.UTF8.GetBytes(html));

    private static string Document(string body)
        => $"<!DOCTYPE html><html><head><meta charset=\"utf-8\"></head><body>{body}</body></html>";
}
