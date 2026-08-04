using Regira.Entities.Web.Models.Abstractions;

namespace Regira.Entities.Web.Models;

public record ListResult<T> : IEntityResult
{
    public long? Duration { get; set; }
    public IList<T> Items { get; set; } = null!;
}