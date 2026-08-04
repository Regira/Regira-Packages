namespace Regira.DAL.Paging;

public static class QueryExtensions
{
    /// <summary>
    /// Applies paging to the specified query based on the provided <see cref="PagingInfo"/>.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the query.</typeparam>
    /// <param name="query">The query to which paging will be applied.</param>
    /// <param name="info">The paging information containing the page size and page number. If <c>null</c>, no paging is applied.</param>
    /// <returns>
    /// A query with paging applied if <paramref name="info"/> is not <c>null</c>;
    /// otherwise, the original query.
    /// </returns>
    public static IQueryable<T> PageQuery<T>(this IQueryable<T> query, PagingInfo? info)
        => info == null ? query : query.PageQuery(info.PageSize, info.Page);
    /// <summary>
    /// Applies paging to the specified query based on the provided page size and page number.
    /// </summary>
    /// <typeparam name="T">The type of the elements in the query.</typeparam>
    /// <param name="query">The query to which paging will be applied.</param>
    /// <param name="pageSize">The number of items per page. <c>null</c>, zero, or negative → no paging is applied.</param>
    /// <param name="page">
    /// The page number to retrieve. If less than 1, it defaults to 1.
    /// Pages are 1-based, meaning the first page is page 1.
    /// </param>
    /// <returns>
    /// A query with paging applied if <paramref name="pageSize"/> is greater than 0;
    /// otherwise, the original query.
    /// </returns>
    public static IQueryable<T> PageQuery<T>(this IQueryable<T> query, int? pageSize, int page = 1)
    {
        // make sure page is greater than 0
        page = Math.Max(1, page);

        if (pageSize is > 0)
        {
            if (page > 1)
            {
                query = query.Skip((page - 1) * pageSize.Value);
            }

            return query.Take(pageSize.Value);
        }

        return query;
    }
}