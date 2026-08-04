namespace Regira.Entities.Models.Abstractions;

public interface IHasCreated
{
    /// <summary>
    /// Creation timestamp, in UTC by default (see <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/>).<br />
    /// Set by <c>HasCreatedDbPrimer</c> on insert; client-supplied values are normalized to UTC.
    /// </summary>
    public DateTime Created { get; set; }
}
public interface IHasLastModified
{
    /// <summary>
    /// Last modification timestamp, in UTC by default (see <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/>).<br />
    /// Set by <c>HasLastModifiedDbPrimer</c> on update; <c>null</c> until the entity is modified.
    /// </summary>
    public DateTime? LastModified { get; set; }
}

public interface IHasTimestamps : IHasCreated, IHasLastModified;
