using System.Net.Mime;
using Microsoft.AspNetCore.Http;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;

namespace Regira.Web.Extensions;

public static class NamedFileResultExtensions
{
    /// <summary>
    /// The minimal-API counterpart of <see cref="ControllerExtensions.File"/>: serves <paramref name="file"/> as its
    /// stored type, inline or as a download, with the same headers; a file whose content is missing answers a
    /// <c>ProblemDetails</c> 404, as an <c>[ApiController]</c>'s <c>NotFound()</c> does.
    /// </summary>
    public static IResult ToFileResult(this INamedFile file, HttpContext httpContext, bool inline = true)
    {
        var stream = NamedFileResponse.Prepare(httpContext.Response, file, inline, out var contentType);
        if (stream == null)
        {
            return Results.Problem(statusCode: StatusCodes.Status404NotFound);
        }
        return Results.File(stream, contentType, inline ? null : file.FileName);
    }
}

/// <summary>The response headers and the stream both download helpers serve a named file with.</summary>
internal static class NamedFileResponse
{
    /// <returns>The file's content, rewound; <c>null</c> when it has none, and then no header is written.</returns>
    public static Stream? Prepare(HttpResponse response, INamedFile file, bool inline, out string contentType)
    {
        // Resolved before any header is written: a missing blob must return a clean 404, not one advertising
        // a Content-Disposition for a file that is not there.
        // (GetStream() copies a stream-backed file into a MemoryStream, so this does buffer — it is a
        // fresh, rewound, independently disposable stream, not a zero-copy handover.)
        var stream = file.GetStream();
        contentType = string.Empty;
        if (stream == null)
        {
            return null;
        }

        var disposition = new ContentDisposition
        {
            FileName = file.FileName,
            Inline = inline
        };
        response.Headers["Content-Disposition"] = disposition.ToString();
        // make content-disposition available to a client
        response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
        // the file is served as its stored type: a browser must not guess another from its bytes
        response.Headers["X-Content-Type-Options"] = "nosniff";
        // Belt-and-braces: GetStream() already hands back a rewound stream. A file result sends
        // Content-Length = stream.Length but copies from the current position, so were that ever not the
        // case the body would be truncated (or empty) with a correct Content-Length.
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        contentType = !string.IsNullOrWhiteSpace(file.ContentType)
            ? file.ContentType
            : ContentTypeUtility.GetContentType(file.FileName);
        // a file that renders as a page runs its scripts on this origin — an upload named .html or .svg, or a type a client
        // declared before uploads were typed by name. The sandbox lets any file render and run nothing; only a PDF goes
        // without, since a sandbox keeps the browser's PDF viewer from loading. Appended, not set: a browser enforces every
        // policy it receives, so one the app already sent still applies.
        if (!contentType.Split(';')[0].Trim().Equals("application/pdf", StringComparison.OrdinalIgnoreCase))
        {
            response.Headers.Append("Content-Security-Policy", "sandbox");
        }
        return stream;
    }
}
