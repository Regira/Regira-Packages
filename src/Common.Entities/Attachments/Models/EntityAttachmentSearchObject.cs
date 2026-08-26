using Regira.Entities.Attachments.Abstractions;
using Regira.Entities.Models;

namespace Regira.Entities.Attachments.Models;

public record EntityAttachmentSearchObject : EntityAttachmentSearchObject<int, int>;
public record EntityAttachmentSearchObject<TKey, TObjectKey> : SearchObject<TKey>, IEntityAttachmentSearchObject<TKey, TObjectKey>
{
    public ICollection<TObjectKey>? ObjectId { get; set; }
    public string? Title { get; set; }
    public string? FileName { get; set; }
    /// <summary>
    /// A file extension, not a pattern: "pdf", ".pdf" and "*.pdf" all mean the same thing, and the filter
    /// anchors on the separating dot, so "pdf" matches "report.pdf" but not "handbook-nopdf". Wildcards are
    /// only stripped from the ends, so an interior one ("p*f") reaches the query as a literal and matches
    /// nothing.
    /// </summary>
    public string? Extension { get; set; }
    public long? MinSize { get; set; }
    public long? MaxSize { get; set; }
}