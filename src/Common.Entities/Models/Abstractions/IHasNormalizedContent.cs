namespace Regira.Entities.Models.Abstractions;

public interface IHasNormalizedContent
{
    string? NormalizedContent { get; set; }
}

public interface IHasLastNormalized : IHasNormalizedContent, IHasLastModified
{
    DateTime? LastNormalized { get; set; }
}