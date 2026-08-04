using System.ComponentModel.DataAnnotations;
using Regira.Entities.Models.Abstractions;
using Regira.Normalizing;

namespace Entities.Providers.Testing.Infrastructure;

/// <summary>
/// A small entity that implements several capability interfaces so the same query suite exercises
/// the divergence hotspots (interface-cast sorting, Q LIKE on normalized content, archive global filter)
/// against every provider.
/// </summary>
public class Widget : IEntity<int>, IHasNormalizedTitle, IHasNormalizedContent, IArchivable, IHasCreated
{
    public int Id { get; set; }

    [StringLength(128)]
    public string? Title { get; set; }

    // Populated by the normalizer interceptor from Title (see Course in Testing.Library for the same pattern).
    [MaxLength(128)]
    [Normalized(SourceProperty = nameof(Title))]
    public string? NormalizedTitle { get; set; }

    [StringLength(512)]
    public string? Description { get; set; }

    // Aggregated + normalized from Title and Description by the normalizer interceptor; this is what
    // Q searches over via EF.Functions.Like. Without the [Normalized] attribute it would stay empty.
    [MaxLength(1024)]
    [Normalized(SourceProperties = [nameof(Title), nameof(Description)])]
    public string? NormalizedContent { get; set; }

    public bool IsArchived { get; set; }

    public DateTime Created { get; set; }
}
