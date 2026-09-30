using Regira.IO.Utilities;
using Regira.Office.Models;

namespace Regira.Office.MimeTypes;

/// <summary>
/// The content type of a Word conversion's output, named by the format produced — one table for every backend with a
/// document model, so their <c>Convert</c> results cannot disagree.
/// </summary>
internal static class WordContentTypes
{
    public static string Of(FileFormat format)
        => format switch
        {
            FileFormat.Pdf => ContentTypes.PDF,
            FileFormat.Html => ContentTypes.HTML,
            FileFormat.Doc or FileFormat.Dot => ContentTypes.DOC,
            FileFormat.Docx or FileFormat.Dotx or FileFormat.Docm or FileFormat.Dotm => ContentTypes.DOCX,
            FileFormat.Odt => "application/vnd.oasis.opendocument.text",
            FileFormat.EPub => "application/epub+zip",
            _ => ContentTypeUtility.GetContentType($"x.{format.ToString().ToLowerInvariant()}")
        };
}
