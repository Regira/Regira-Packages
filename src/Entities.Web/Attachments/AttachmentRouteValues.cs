using Regira.Entities.Attachments.Extensions;

namespace Regira.Entities.Web.Attachments;

internal static class AttachmentRouteValues
{
    /// <summary>
    /// The file name of a <c>{objectId}/files/{*fileName}</c> download as stored. ASP.NET Core does not decode %2F in a path
    /// segment (it would change the route's shape), and LinkGenerator emits exactly that form for the Uri on the DTO — so an
    /// encoded link arrives as "archive%2F2026%2Fscan.txt" and must be decoded here. A literal path arrives already split and
    /// is untouched. Normalizing after decoding means both spellings hit the same stored value.
    /// </summary>
    public static string DecodeFileName(string fileName)
        => (fileName.Contains("%2F", StringComparison.OrdinalIgnoreCase)
            ? Uri.UnescapeDataString(fileName)
            : fileName).ToVirtualPath()!;
}
