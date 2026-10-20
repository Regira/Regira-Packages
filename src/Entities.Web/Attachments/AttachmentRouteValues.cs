using Regira.DAL.Paging;
using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Attachments.Extensions;
using Regira.Entities.Attachments.Models;
using Regira.Entities.Keywords;
using Regira.Entities.Keywords.Abstractions;
using Regira.Entities.Mediator;
using Regira.Entities.Services.Abstractions;

namespace Regira.Entities.Web.Attachments;

internal static class AttachmentRouteValues
{
    /// <summary>
    /// The id of owner <paramref name="objectId"/>'s link whose file is named <paramref name="fileName"/>, as a
    /// <c>{objectId}/files/{*fileName}</c> download routes it; <c>null</c> when there is none. Both downloads then send
    /// <c>AttachmentFileQuery</c>, so one handler override covers them. A name is one file: one left empty (<c>/</c>) or
    /// blank drops the file-name filter, and a wildcard makes it a pattern, so both name none.
    /// </summary>
    public static async Task<int?> FindLinkId<TEntity>(IServiceProvider services, int objectId, string fileName, CancellationToken token = default)
        where TEntity : class, IEntityAttachment<int, int, int, Attachment>
    {
        var name = DecodeFileName(fileName);
        var keywords = (IQKeywordHelper?)services.GetService(typeof(IQKeywordHelper)) ?? new QKeywordHelper();
        if (string.IsNullOrWhiteSpace(name) || keywords.ParseKeyword(name).HasWildcard)
        {
            return null;
        }
        var service = services.GetRequiredEntityService<IEntityService<TEntity, int>>();
        var links = await service.List(new { objectId = new[] { objectId }, fileName = name }, new PagingInfo { PageSize = 1 }, token);
        return links.FirstOrDefault()?.Id;
    }

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
