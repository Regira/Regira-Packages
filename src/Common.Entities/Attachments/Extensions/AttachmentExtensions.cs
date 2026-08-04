using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Services;
using Regira.IO.Abstractions;

namespace Regira.Entities.Attachments.Extensions;

public static class AttachmentExtensions
{
    /// <summary>
    /// Normalizes a client-supplied file name that may carry a <b>virtual folder</b>
    /// (<c>"folder1/folder2/report.pdf"</c>): forward slashes, no leading or trailing separator, and empty or
    /// relative (<c>.</c> / <c>..</c>) segments dropped.
    /// <para>
    /// The folder is kept — it is how the client organises attachments, and re-filing one is nothing more than
    /// writing a different <c>FileName</c>. It never reaches storage: the identifier is generated independently,
    /// so a "move" never touches bytes. Normalizing matters because this value is echoed into the download URL
    /// and matched verbatim by the <c>{objectId}/files/{*fileName}</c> route.
    /// </para>
    /// </summary>
    public static string? ToVirtualPath(this string? fileName)
    {
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return fileName;
        }

        var segments = fileName
            .Split(['/', '\\'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(segment => segment != "." && segment != "..")
            .ToArray();

        return segments.Length == 0 ? null : string.Join('/', segments);
    }

    /// <summary>
    /// The last segment of a virtual path — the display name without its folders. Recognises both separator
    /// styles on every platform, unlike <c>Path.GetFileName</c>, which only honours the host's.
    /// </summary>
    public static string? ToFileNameOnly(this string? fileName)
        => string.IsNullOrWhiteSpace(fileName)
            ? fileName
            : fileName[(fileName.LastIndexOfAny(['/', '\\']) + 1)..];

    public static Attachment ToAttachment(this INamedFile file, Attachment? src = null)
        => file.ToAttachment<Attachment, int>();
    public static TAttachment ToAttachment<TAttachment, TKey>(this INamedFile file, TAttachment? src = null)
        where TAttachment : class, IAttachment<TKey>, new()
    {
        src ??= new TAttachment();
        src.Bytes = file.Bytes;
        src.Stream = file.Stream;
        src.Length = file.Length;
        src.ContentType = file.ContentType;
        src.FileName = file.FileName.ToVirtualPath();
        return src;
    }
    public static TAttachment ToAttachment<TAttachment, TKey>(this IBinaryFile file, TAttachment? src = null)
        where TAttachment : class, IAttachment<TKey>, new()
    {
        var attachment = ((INamedFile)file).ToAttachment<TAttachment, TKey>(src);
        attachment.Identifier = file.Identifier;
        attachment.Path = file.Path;
        attachment.Prefix = file.Prefix;
        return attachment;
    }


    public static EntityAttachmentSearchObject? ToSearchObject(this object? so)
        => SearchObjectCoercion.Coerce<EntityAttachmentSearchObject>(so);
}