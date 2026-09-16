using Regira.Office.PDF.Models;

namespace Regira.Office.Word.Gotenberg;

/// <summary>
/// Configuration for <see cref="WordService"/>.
/// </summary>
public class GotenbergWordConfig
{
    /// <summary>
    /// Base URL of the Gotenberg server, e.g. <c>http://localhost:3000</c>. Include the server's
    /// <c>--api-root-path</c> when it is not <c>/</c>.
    /// </summary>
    public string BaseUrl { get; set; } = null!;

    /// <summary>
    /// Client-side limit per request. When left empty, <see cref="HttpClient"/>'s default (100 seconds) applies.
    /// <para>
    /// Gotenberg enforces its own limit (<c>--api-timeout</c>, 30 seconds by default) and answers
    /// <c>503 Service Unavailable</c> when a conversion exceeds it. Raise both for large documents.
    /// </para>
    /// </summary>
    public TimeSpan? Timeout { get; set; }

    /// <summary>
    /// Basic-auth user name, for a server started with <c>--api-enable-basic-auth</c>
    /// (<c>GOTENBERG_API_BASIC_AUTH_USERNAME</c> on the server side).
    /// </summary>
    public string? Username { get; set; }

    /// <summary>
    /// Basic-auth password (<c>GOTENBERG_API_BASIC_AUTH_PASSWORD</c> on the server side).
    /// </summary>
    public string? Password { get; set; }

    /// <summary>
    /// Image size and format for <see cref="WordService.ToImages"/>, handed to the
    /// <see cref="Regira.Office.PDF.Abstractions.IPdfToImageService"/> that rasterises the PDF.
    /// When left empty, that service's defaults apply.
    /// </summary>
    public PdfToImagesOptions? ImageOptions { get; set; }
}
