using System.Net.Mime;
using Microsoft.AspNetCore.Mvc;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;

namespace Regira.Web.Extensions;

public static class ControllerExtensions
{
    public static IActionResult File(this ControllerBase ctrl, INamedFile file, bool inline = true)
    {
        var disposition = new ContentDisposition
        {
            FileName = file.FileName,
            Inline = inline
        };
        ctrl.Response.Headers["Content-Disposition"] = disposition.ToString();
        // make content-disposition available for axios client
        ctrl.Response.Headers["Access-Control-Expose-Headers"] = "Content-Disposition";
        // the stored ContentType may be user-supplied on upload; don't let browsers sniff around it
        ctrl.Response.Headers["X-Content-Type-Options"] = "nosniff";

        // stream instead of buffering the whole file in memory
        var stream = file.GetStream();
        if (stream == null)
        {
            return ctrl.NotFound();
        }
        // FileStreamResult sends Content-Length = stream.Length but copies from the current
        // position, so a non-rewound stream yields a truncated (or empty) body
        if (stream.CanSeek)
        {
            stream.Position = 0;
        }
        var contentType = !string.IsNullOrWhiteSpace(file.ContentType)
            ? file.ContentType
            : ContentTypeUtility.GetContentType(file.FileName);
        return ctrl.File(stream, contentType, inline ? null : file.FileName);
    }
}