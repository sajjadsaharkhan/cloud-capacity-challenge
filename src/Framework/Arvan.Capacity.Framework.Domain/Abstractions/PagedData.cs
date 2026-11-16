namespace Arvan.Capacity.Framework.Domain.Abstractions;

/// <summary>
/// One page of results with the metadata describing where it sits. Used unchanged from the
/// repository that reads it to the response that serializes it — the pagination travels inside the
/// payload rather than beside it, so a list result is one object at every layer.
/// </summary>
/// <typeparam name="T">The item type on this page.</typeparam>
/// <param name="Items">The items on this page.</param>
/// <param name="Pagination">Where this page sits in the full result set.</param>
public sealed record PagedData<T>(IReadOnlyList<T> Items, PaginationMeta Pagination)
{
    /// <summary>
    /// Builds a page from a query's results, deriving every count in
    /// <see cref="PaginationMeta"/> from the total.
    /// </summary>
    /// <param name="items">The items read for this page.</param>
    /// <param name="totalCount">The number of items the query matches across every page.</param>
    /// <param name="pageNumber">The one-based number of this page.</param>
    /// <param name="pageSize">The maximum number of items a page holds.</param>
    public static PagedData<T> Create(IReadOnlyList<T> items, int totalCount, int pageNumber, int pageSize) =>
        new(items, PaginationMeta.For(totalCount, pageNumber, pageSize));

    /// <summary>
    /// Projects each item to another shape, carrying this page's
    /// <see cref="PaginationMeta"/> across unchanged.
    /// </summary>
    /// <remarks>
    /// This is what every layer boundary uses — entity to Output, Output to Response — so no
    /// mapper has to restate the pagination arithmetic, and none can get it wrong.
    /// </remarks>
    /// <typeparam name="TTarget">The shape to project each item to.</typeparam>
    /// <param name="map">The per-item projection.</param>
    public PagedData<TTarget> Map<TTarget>(Func<T, TTarget> map) =>
        new([.. Items.Select(map)], Pagination);
}

/// <summary>
/// Where a page sits in the full result set. Every member is derived at construction, so no two
/// of them can disagree.
/// </summary>
/// <param name="PageNumber">The one-based number of this page.</param>
/// <param name="PageSize">The maximum number of items a page holds.</param>
/// <param name="TotalCount">The number of items across every page.</param>
/// <param name="TotalPages">The number of pages the full result set spans.</param>
/// <param name="HasNextPage">Whether a page follows this one.</param>
/// <param name="HasPreviousPage">Whether a page precedes this one.</param>
public sealed record PaginationMeta(
    int PageNumber,
    int PageSize,
    int TotalCount,
    int TotalPages,
    bool HasNextPage,
    bool HasPreviousPage)
{
    /// <summary>
    /// Derives the page metadata for a query that matched <paramref name="totalCount"/> items.
    /// </summary>
    /// <param name="totalCount">The number of items the query matches across every page.</param>
    /// <param name="pageNumber">The one-based number of this page.</param>
    /// <param name="pageSize">The maximum number of items a page holds.</param>
    public static PaginationMeta For(int totalCount, int pageNumber, int pageSize)
    {
        var totalPages = pageSize > 0 ? (int)Math.Ceiling(totalCount / (double)pageSize) : 0;

        return new PaginationMeta(
            pageNumber,
            pageSize,
            totalCount,
            totalPages,
            HasNextPage: pageNumber < totalPages,
            HasPreviousPage: pageNumber > 1);
    }
}
