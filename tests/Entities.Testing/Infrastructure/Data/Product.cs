using Regira.Entities.Models;
using Regira.Entities.Models.Abstractions;
using Regira.Normalizing;
using System.ComponentModel.DataAnnotations;

namespace Entities.Testing.Infrastructure.Data;

public class Product : IEntity<int>, IHasNormalizedTitle, IHasDescription, IHasTimestamps
{
    public int Id { get; set; }
    public int? CategoryId { get; set; }
    [MaxLength(64)]
    public string? Title { get; set; }
    [MaxLength(64)]
    [Normalized(SourceProperty = nameof(Title))]
    public string? NormalizedTitle { get; set; }
    [MaxLength(512)]
    public string? Description { get; set; }
    public DateTime Created { get; set; }
    public DateTime? LastModified { get; set; }

    public Category? Category { get; set; }
    public ICollection<ProductTag>? Tags { get; set; }
}

public record ProductSearchObject : SearchObject
{
    public ICollection<int>? CategoryId { get; set; }
}