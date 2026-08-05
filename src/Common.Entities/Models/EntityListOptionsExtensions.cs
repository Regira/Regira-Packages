using Regira.DAL.Paging;

namespace Regira.Entities.Models;

public static class EntityListOptionsExtensions
{
    /// <summary>
    /// Resolves the effective paging for a list/search request from <paramref name="options"/> —
    /// the single clamp algorithm applied at the HTTP boundary (used by the MVC controllers; any
    /// other HTTP surface can reuse it).
    /// An omitted <c>PageSize</c> (<c>null</c>) falls back to <see cref="EntityListOptions.DefaultPageSize"/>;
    /// a non-positive <c>PageSize</c> (an explicit opt-out) falls back to <see cref="EntityListOptions.MaxPageSize"/>;
    /// a positive value is honoured. <c>MaxPageSize</c> is always the ceiling. <see cref="PagingInfo"/> is a
    /// record, so <c>with</c> preserves <c>Page</c>.
    /// </summary>
    public static PagingInfo? ApplyPagingDefaults(this PagingInfo? pagingInfo, EntityListOptions? options)
    {
        var defaultPageSize = options?.DefaultPageSize;
        var maxPageSize = options?.MaxPageSize;

        var requested = pagingInfo?.PageSize;
        int? size;
        if (requested is > 0) size = requested;                                       // explicit positive page size
        else if (requested is null && defaultPageSize is > 0) size = defaultPageSize; // omitted → configured default
        else size = maxPageSize;                                                      // explicit <= 0 (opt-out), or omitted with no default → the max
        if (maxPageSize is > 0 && size > maxPageSize) size = maxPageSize;             // never exceed the max
        if (size is not > 0) return pagingInfo;                                       // nothing configured → paging off (service returns everything)
        return pagingInfo is null ? new PagingInfo { PageSize = size } : pagingInfo with { PageSize = size };
    }
}
