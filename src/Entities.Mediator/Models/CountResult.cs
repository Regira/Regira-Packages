using Regira.Entities.Web.Models.Abstractions;

namespace Regira.Entities.Web.Models;

public record CountResult : IEntityResult
{
    public long? Duration { get; set; }
    public int Count { get; set; }
}