namespace Regira.Entities.Models.Abstractions;

public interface ISearchObject
{
    string? Q { get; set; }

    /// <summary>
    /// Timestamp range filters, interpreted as UTC by default (see <see cref="Regira.Utilities.DateTimeDefaults.UseUtc"/>).<br />
    /// Values with <see cref="DateTimeKind.Local"/> are converted, <see cref="DateTimeKind.Unspecified"/> values are assumed to be UTC.
    /// </summary>
    DateTime? MinCreated { get; set; }
    /// <inheritdoc cref="MinCreated"/>
    DateTime? MaxCreated { get; set; }
    /// <inheritdoc cref="MinCreated"/>
    DateTime? MinLastModified { get; set; }
    /// <inheritdoc cref="MinCreated"/>
    DateTime? MaxLastModified { get; set; }

    /// <summary>
    /// Which rows of an <c>IArchivable</c> entity to return: <c>Excluded</c> (archived invisible),
    /// <c>Included</c> (live + archived) or <c>Only</c> (archived only). <c>null</c> falls back to the
    /// configured <see cref="EntityQueryOptions.DefaultArchivedFilter"/>.<br />
    /// Read by the built-in <c>IArchivable</c> filter only — every other global filter (tenant/owner row
    /// security) keeps running untouched, and so does every EF query filter the application configured
    /// itself, so this widens the result by the archived flag and by nothing else.
    /// </summary>
    ArchivedFilter? Archived { get; set; }
}

public interface ISearchObject<TKey> : ISearchObject
{
    TKey? Id { get; set; }
    ICollection<TKey>? Ids { get; set; }
    ICollection<TKey>? Exclude { get; set; }
}