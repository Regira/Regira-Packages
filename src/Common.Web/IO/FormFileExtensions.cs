using Microsoft.AspNetCore.Http;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;
using Regira.IO.Utilities;

namespace Regira.Web.IO;

public static class FormFileExtensions
{
    /// <summary>
    /// The uploaded file, its content type named by its file name's extension — <c>application/octet-stream</c> when the
    /// extension is unknown. The type the client declared is not used: a store or a link serves the file with the type
    /// it holds, so a <c>.png</c> declared <c>text/html</c> would be served as a page, past any check on the extension.
    /// </summary>
    public static INamedFile ToNamedFile(this IFormFile formFile)
    {
        // don't use using here so the stream is not disposed (will be disposed with INamedFile.Dispose)
        var fileStream = formFile.OpenReadStream();

        var file = fileStream.ToBinaryFile();
        file.FileName = formFile.FileName;
        file.ContentType = ContentTypeUtility.GetContentType(formFile.FileName);
        file.Length = formFile.Length;

        return file;
    }
}