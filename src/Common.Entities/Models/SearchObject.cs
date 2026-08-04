using Regira.Entities.Models.Abstractions;

namespace Regira.Entities.Models;

public record SearchObject : SearchObject<int>;
public record SearchObject<TKey> : ISearchObject<TKey>
{
    public TKey? Id { get; set; }
    public ICollection<TKey>? Ids { get; set; }
    public ICollection<TKey>? Exclude { get; set; }
    public string? Q { get; set; }

    public DateTime? MinCreated { get; set; }
    public DateTime? MaxCreated { get; set; }
    public DateTime? MinLastModified { get; set; }
    public DateTime? MaxLastModified { get; set; }

    /// <inheritdoc cref="ISearchObject.Archived"/>
    public ArchivedFilter? Archived { get; set; }
}