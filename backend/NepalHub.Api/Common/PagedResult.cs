namespace NepalHub.Api.Common;

/// <summary>
/// The single paged shape every list endpoint returns. Keeping pagination, sorting
/// and filtering on one convention is what lets the later global-search module reuse
/// it instead of inventing its own.
/// </summary>
public sealed record PagedResult<T>(IReadOnlyList<T> Items, int Page, int PageSize, int TotalCount)
{
    public int TotalPages => PageSize <= 0 ? 0 : (int)Math.Ceiling(TotalCount / (double)PageSize);

    public bool HasPrevious => Page > 1;

    public bool HasNext => Page < TotalPages;
}

/// <summary>Normalised paging arguments, with the page size capped.</summary>
public sealed record PageRequest(int Page, int PageSize)
{
    public const int DefaultPageSize = 24;
    public const int MaxPageSize = 100;

    public int Skip => (Page - 1) * PageSize;

    /// <summary>
    /// Clamps whatever the caller sent. A page below 1 becomes the first page, and a
    /// page size above <see cref="MaxPageSize"/> is reduced rather than rejected, so a
    /// client cannot ask the database for an unbounded result set.
    /// </summary>
    public static PageRequest Normalise(int? page, int? pageSize)
    {
        var normalisedPage = page is null || page < 1 ? 1 : page.Value;
        var requested = pageSize is null || pageSize < 1 ? DefaultPageSize : pageSize.Value;

        return new PageRequest(normalisedPage, Math.Min(requested, MaxPageSize));
    }
}
