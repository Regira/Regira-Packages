namespace Regira.DAL.Paging;

/// <summary>
/// Represents the paging information used for paginated data retrieval.
/// </summary>
public record PagingInfo
{
    /// <summary>
    /// Items per page. <c>null</c> = the caller omitted paging (the List/Search endpoints fall back to the
    /// configured default page size); <c>0</c> or negative = opt out of paging (the endpoints return every
    /// row, capped at the configured maximum). A direct <c>IEntityService</c> call applies the value as-is
    /// and is never capped.
    /// </summary>
    public int? PageSize { get; set; }
    /// <summary>
    /// The current page number to be retrieved. Default is 1.
    /// </summary>
    public int Page { get; set; } = 1;
}