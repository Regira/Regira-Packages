using Regira.Entities.Web.Models.Abstractions;

namespace Regira.Entities.Web.Models;

public record DeleteResult<T> : IEntityResult
{
    public long? Duration { get; set; }
    /// <summary>
    /// Rows written by the delete. A soft delete (<c>IArchivable</c>) updates the row rather than removing
    /// it, so this stays the real <c>SaveChanges</c> count.
    /// </summary>
    public int Affected { get; set; }
    public T Item { get; set; } = default!;
}