using Microsoft.AspNetCore.Mvc;
using Regira.IO.Abstractions;

namespace Regira.Web.Extensions;

public static class ControllerExtensions
{
    /// <summary>
    /// Serves <paramref name="file"/> as its stored type, inline or as a download, with the headers that keep a file
    /// from running on this origin (see <see cref="NamedFileResultExtensions.ToFileResult"/>, the minimal-API counterpart);
    /// a file whose content is missing answers 404.
    /// </summary>
    public static IActionResult File(this ControllerBase ctrl, INamedFile file, bool inline = true)
    {
        var stream = NamedFileResponse.Prepare(ctrl.Response, file, inline, out var contentType);
        if (stream == null)
        {
            return ctrl.NotFound();
        }
        return ctrl.File(stream, contentType, inline ? null : file.FileName);
    }
}
