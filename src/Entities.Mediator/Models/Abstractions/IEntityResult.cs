namespace Regira.Entities.Web.Models.Abstractions;

public interface IEntityResult
{
    /// <summary>
    /// Duration in ms
    /// </summary>
    long? Duration { get; set; }
}