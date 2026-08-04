using Microsoft.AspNetCore.Http;
using Regira.IO.Abstractions;
using Regira.IO.Extensions;

namespace Regira.Web.IO;

public static class FormFileExtensions
{
    public static INamedFile ToNamedFile(this IFormFile formFile)
    {
        // don't use using here so the stream is not disposed (will be disposed with INamedFile.Dispose)
        var fileStream = formFile.OpenReadStream();

        var file = fileStream.ToBinaryFile();
        file.FileName = formFile.FileName;
        file.ContentType = formFile.ContentType;
        file.Length = formFile.Length;

        return file;
    }
}